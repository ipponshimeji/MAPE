using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;


namespace MAPE.Http {
	public class BodyBuffer: MessageBuffer {
		#region constants

		public const int BodyStreamThreshold = 1024 * 1024;     // 1M

		#endregion


		#region data

		private HeaderBuffer headerBuffer;

		private Stream bodyStream;

		private long bodyLength;

		private bool chunking;

		#endregion


		#region properties

		public bool CanRead {
			get {
				return (this.headerBuffer == null) ? false : this.headerBuffer.CanRead;
			}
		}

		#endregion


		#region creation and disposal

		public BodyBuffer(): base() {
			// initialize members
			this.headerBuffer = null;
			this.bodyStream = null;
			this.bodyLength = 0;
			this.chunking = false;

			return;
		}

		public override void Dispose() {
			// ensure detached
			DetachStream();

			base.Dispose();
		}

		/// <summary>
		/// </summary>
		/// <param name="input"></param>
		/// <remarks>
		/// This object does not own the ownership of <paramref name="input"/>.
		/// That is, this object does not Dispose it in its Detach() call.
		/// </remarks>
		public void AttachStream(HeaderBuffer headerBuffer) {
			// argument checks
			Debug.Assert(headerBuffer != null);

			// state checks
			if (this.headerBuffer != null) {
				throw new InvalidOperationException("This object already attached streams.");
			}

			// set the headerBuffer
			this.headerBuffer = headerBuffer;
			// the buffer state should be 'reset' state 
			Debug.Assert(this.chunking == false);
			Debug.Assert(this.bodyLength == 0);
			Debug.Assert(this.bodyStream == null);
			Debug.Assert(this.Limit == 0);
			Debug.Assert(this.Next == 0);

			return;
		}

		public void DetachStream() {
			// state checks
			if (this.headerBuffer == null) {
				// nothing to do
				return;
			}

			// do not dispose the headerBuffer, just clear it
			// This object does not have the ownership of it.
			this.headerBuffer = null;

			// reset buffer state
			ResetBuffer();

			return;
		}

		#endregion


		#region methods - read

		public void SkipBody(long contentLength) {
			// argument checks
			if (contentLength < 0) {
				throw new ArgumentOutOfRangeException(nameof(contentLength));
			}

			// state checks
			HeaderBuffer headerBuffer = this.headerBuffer;
			if (headerBuffer == null) {
				throw new InvalidOperationException();
			}
			if (this.bodyLength != 0) {
				throw new InvalidOperationException("This buffer has already handled a message body.");
			}
			Debug.Assert(this.bodyStream == null);

			// skip the body storing its bytes 
			// The media to store body depends on its length and the current margin.
			Stream bodyStream = null;
			try {
				// Note that the some body bytes may be read into the header buffer.
				// Unread bytes in the header buffer at this point are body bytes.
				// That is, range [headerBuffer.Next - headerBuffer.Limit).
				int bodyBytesInHeaderBufferLength = headerBuffer.Limit - headerBuffer.Next;
				long restLen = contentLength - bodyBytesInHeaderBufferLength;
				if (restLen <= headerBuffer.Margin) {
					// The body is to be stored in the rest of header buffer.

					// read body bytes into the rest of the header buffer
					Debug.Assert(restLen <= int.MaxValue);
					FillBuffer(headerBuffer, (int)restLen);
					Debug.Assert(contentLength == headerBuffer.Limit - headerBuffer.Next);
					Debug.Assert(this.MemoryBlock == null);
				} else {
					byte[] memoryBlock = EnsureMemoryBlockAllocated();
					if (contentLength <= memoryBlock.Length) {
						// The body is to be stored in a memory block.

						// copy body bytes in the header buffer to this buffer
						CopyFrom(headerBuffer, headerBuffer.Next, bodyBytesInHeaderBufferLength);

						// read rest of body bytes
						Debug.Assert(restLen <= int.MaxValue);
						FillBuffer((int)restLen);
						Debug.Assert(contentLength == (this.Limit - this.Next));
					} else {
						// The body is to be stored in a stream.

						// determine which medium is used to store body, memory or file 
						if (contentLength <= BodyStreamThreshold) {
							// use memory stream
							Debug.Assert(contentLength <= int.MaxValue);
							bodyStream = new MemoryStream((int)contentLength);
						} else {
							// use temp file stream
							bodyStream = CreateTempFileStream();
						}

						// write body bytes in the header buffer to the bodyStream
						WriteTo(headerBuffer, bodyStream, headerBuffer.Next, bodyBytesInHeaderBufferLength);

						// write rest of body bytes to the bodyStream
						// the memoryBlock is used as just intermediate buffer instead of storing media
						long amount = bodyBytesInHeaderBufferLength;
						while (amount < contentLength) {
							int readCount = ReadBytes(memoryBlock, 0, memoryBlock.Length);
							Debug.Assert(0 < readCount);    // ReadBytes() throws an exception on end of stream 
							bodyStream.Write(memoryBlock, 0, readCount);
							amount += readCount;
						}
					}
				}

				// update state
				this.bodyLength = contentLength;
				this.bodyStream = bodyStream;
			} catch {
				if (bodyStream != null) {
					bodyStream.Dispose();
				}
				throw;
			}

			return;
		}

		public void SkipChunkedBody() {
			// skip the body storing its bytes 
			this.bodyStream = CreateTempFileStream();
			this.chunking = true;	// enter chunking mode
			try {
				// in chunking mode
				StringBuilder stringBuf = new StringBuilder();

				Func<int> readChunkSizeLine = () => {
					// read chunk-size
					stringBuf.Clear();
					bool endOfLine = ReadASCIITo(SP, HTAB, SemiColon, stringBuf, decapitalize: false);
					string stringValue = stringBuf.ToString();

					// skip the rest of chunk-size line
					if (endOfLine == false) {
						SkipToCRLF();
					}

					// parse chunk-size
					int intValue;
					if (int.TryParse(stringValue, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out intValue) == false) {
						throw CreateBadRequestException();
					}

					return intValue;
				};


				// copy body bytes in the header buffer to this buffer
				byte[] memoryBlock = EnsureMemoryBlockAllocated();
				CopyFrom(this.headerBuffer, this.headerBuffer.Next, this.headerBuffer.Limit - this.headerBuffer.Next);

				// skip chunked data
				int chunkSize;
				while (0 < (chunkSize = readChunkSizeLine())) {
					// skip chunk-data + CRLF
					Skip(chunkSize);
					if (ReadNextByte() != CR || ReadNextByte() != LF) {
						throw CreateBadRequestException();
					}
				}

				// skip trailer-part + CRLF
				while (SkipToCRLF() == false) {
					;
				}

				// flush the data currently on the memoryBlock
				this.bodyStream.Write(memoryBlock, 0, this.Next);
				this.bodyLength += this.Next;
			} catch {
				this.bodyLength = 0;
				Stream temp = this.bodyStream;
				this.bodyStream = null;
				if (temp != null) {
					temp.Dispose();
				}
				throw;
			} finally {
				this.chunking = false;
			}

			return;
		}

		#endregion


		#region methods - write

		public void WriteBody(Stream output) {
			// argument checks
			if (output == null) {
				throw new ArgumentNullException(nameof(output));
			}
			if (output.CanWrite == false) {
				throw new ArgumentException("It is not writable", nameof(output));
			}

			// state checks
			if (this.bodyLength == 0) {
				// no body
				output.Flush();
				return;
			}

			// write the body
			// Note the media where the body is stored depends on its size.  
			if (this.bodyStream != null) {
				// the body was stored in the stream (large body or chunked body)
				this.bodyStream.Seek(0, SeekOrigin.Begin);
				this.bodyStream.CopyTo(output);
			} else {
				Debug.Assert(0 <= this.bodyLength && this.bodyLength <= int.MaxValue);
				int bodyLengthInInt = (int)this.bodyLength;

				if (this.MemoryBlock != null) {
					// the body was stored in the memoryBlock (small body)
					Debug.Assert(this.Next == 0);
					Debug.Assert(this.Limit == bodyLengthInInt);
					WriteTo(output, 0, bodyLengthInInt);
				} else {
					// the body was stored in the rest of the header buffer (tiny body)
					HeaderBuffer headerBuffer = this.headerBuffer;
					Debug.Assert(headerBuffer.Limit - headerBuffer.Next == bodyLengthInInt);
					WriteTo(headerBuffer, output, headerBuffer.Next, bodyLengthInInt);
				}
			}
			output.Flush();

			return;
		}

		#endregion


		#region overrides/overridables

		public override void ResetBuffer() {
			// reset this class level
			this.chunking = false;
			this.bodyLength = 0;
			Stream temp = this.bodyStream;
			this.bodyStream = null;
			if (temp != null) {
				temp.Dispose();
			}

			// reset the base class level
			base.ResetBuffer();
		}

		protected override byte[] UpdateMemoryBlock(byte[] currentMemoryBlock) {
			if (this.chunking && currentMemoryBlock != null) {
				// flush the bytes in the currentMemoryBlock to the bodyStream 
				Debug.Assert(this.bodyStream != null);
				this.bodyStream.Write(currentMemoryBlock, 0, this.Limit);
				this.bodyLength += this.Limit;
			}

			return base.UpdateMemoryBlock(currentMemoryBlock);
		}

		protected override int ReadBytes(byte[] buffer, int offset, int count) {
			// state checks
			if (this.headerBuffer == null) {
				throw new InvalidOperationException("No input stream is attached to this object.");
			}

			// You cannot call headerBuffer.ReadBytes() directly because of accessibility.
			// So use the bridge method defined in Buffer class. 
			return ReadBytes(this.headerBuffer, buffer, offset, count);
		}

		#endregion


		#region privates - general

		private static FileStream CreateTempFileStream() {
			string tempFilePath = Path.GetTempFileName();
			try {
				return new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
			} catch {
				try {
					File.Delete(tempFilePath);
				} catch {
					// continue
				}
				throw;
			}
		}

		#endregion
	}
}
