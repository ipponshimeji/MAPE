using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MAPE.Utils;


namespace MAPE.Http {
	public class BodyBuffer: MessageBuffer {
		#region constants

		public const int BodyStreamThreshold = 1024 * 1024;     // 1M

		#endregion


		#region data

		private HeaderBuffer headerBuffer = null;

		private Stream bodyStream = null;

		private long bodyLength = 0;

		private Stream chunkingOutput = null;

		private int unflushedStart = 0;

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
			Debug.Assert(this.chunkingOutput == null);
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
					// The body is to be stored in the rest of header buffer. (tiny body)

					// read body bytes into the rest of the header buffer
					Debug.Assert(restLen <= int.MaxValue);
					FillBuffer(headerBuffer, (int)restLen);
					Debug.Assert(contentLength == headerBuffer.Limit - headerBuffer.Next);
					Debug.Assert(this.MemoryBlock == null);
				} else {
					byte[] memoryBlock = EnsureMemoryBlockAllocated();
					if (contentLength <= memoryBlock.Length) {
						// The body is to be stored in a memory block. (small body)

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
							// use memory stream (medium body)
							Debug.Assert(contentLength <= int.MaxValue);
							bodyStream = new MemoryStream((int)contentLength);
						} else {
							// use temp file stream (large body)
							bodyStream = Util.CreateTempFileStream();
						}

						StoreBody(bodyStream, contentLength);
					}
				}

				// update state
				this.bodyLength = contentLength;
				this.bodyStream = bodyStream;
			} catch {
				DisposableUtil.DisposeSuppressingErrors(bodyStream);
				throw;
			}

			return;
		}

		private void StoreBody(Stream output, long contentLength) {
			// argument checks
			Debug.Assert(output != null);
			Debug.Assert(output.CanWrite);
			Debug.Assert(0 <= contentLength);

			// state checks
			HeaderBuffer headerBuffer = this.headerBuffer;
			Debug.Assert(headerBuffer != null);

			// write body bytes in the header buffer to the output
			int bodyBytesInHeaderBufferLength = headerBuffer.Limit - headerBuffer.Next;
			WriteTo(headerBuffer, output, headerBuffer.Next, bodyBytesInHeaderBufferLength);

			// write rest of body bytes to the output
			// the memoryBlock is used as just intermediate buffer instead of storing media
			byte[] memoryBlock = EnsureMemoryBlockAllocated();
			long amount = bodyBytesInHeaderBufferLength;
			while (amount < contentLength) {
				int readCount = ReadBytes(memoryBlock, 0, memoryBlock.Length);
				Debug.Assert(0 < readCount);    // ReadBytes() throws an exception on end of stream 
				output.Write(memoryBlock, 0, readCount);
				amount += readCount;
			}

			return;
		}

		public void SkipChunkedBody() {
			// state checks
			if (this.bodyStream != null) {
				throw new InvalidOperationException("It has already read body part.");
			}

			// skip the body storing its bytes 
			Stream output = Util.CreateTempFileStream();
			try {
				StoreChunkedBody(output);
			} catch {
				this.bodyLength = 0;
				DisposableUtil.DisposeSuppressingErrors(output);
				throw;
			}

			// set the stream as the bodyStream
			this.bodyStream = output;

			return;
		}

		private void StoreChunkedBody(Stream output) {
			// argument checks
			Debug.Assert(output != null);
			Debug.Assert(output.CanWrite);

			// state checks
			Debug.Assert(this.chunkingOutput == null);

			// store the chunked body into the output stream
			this.chunkingOutput = output;
			try {
				StringBuilder stringBuf = new StringBuilder();
				// ToDo: convert to inner method in C# 7
				Func<int> readChunkSizeLine = () => {
					// read chunk-size
					stringBuf.Clear();
					bool endOfLine = ReadASCIITo(SP, HTAB, SemiColon, stringBuf, decapitalize: false);
					string stringValue = stringBuf.ToString();

					// skip the rest of chunk-size line (chunk-ext)
					if (endOfLine == false) {
						SkipToCRLF();
					}

					// parse chunk-size
					int intValue;
					if (int.TryParse(stringValue, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out intValue) == false) {
						throw new FormatException($"The format of 'chunk-size' is invalid: {stringValue}");
					}

					return intValue;
				};

				// copy body bytes read in the header buffer into this buffer
				byte[] memoryBlock = EnsureMemoryBlockAllocated();
				CopyFrom(this.headerBuffer, this.headerBuffer.Next, this.headerBuffer.Limit - this.headerBuffer.Next);
				this.unflushedStart = 0;

				// skip chunked data
				// Note that scanned or skipped data are flushed into this.chunkingOutput.
				// (see UpdateMemoryBlock() implementation of this class) 
				int chunkSize;
				while (0 < (chunkSize = readChunkSizeLine())) {
					// skip chunk-data + CRLF
					Skip(chunkSize);
					if (ReadNextByte() != CR || ReadNextByte() != LF) {
						throw new Exception("invalid chunk-data: not followed by CRLF");
					}
					FlushChunkingOutput();
				}

				// skip trailer-part + CRLF
				while (SkipToCRLF() == false) {
					;
				}

				// flush the unflushed data
				FlushChunkingOutput();
			} finally {
				this.chunkingOutput = null;
			}

			return;
		}

		private void FlushChunkingOutput() {
			// state checks
			Stream chunkingOutput = this.chunkingOutput;
			Debug.Assert(chunkingOutput != null);

			byte[] memoryBlock = this.MemoryBlock;
			if (memoryBlock != null) {
				// flush the bytes in the memoryBlock to the chunkingOutput
				int start = this.unflushedStart;
				int end = this.Next;
				int count = end - start;
				if (0 < count) {
					chunkingOutput.Write(memoryBlock, start, count);
					this.bodyLength += count;
					this.unflushedStart = end;
				}

				// flush the chunkingOutput
				chunkingOutput.Flush();
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

		public void RedirectBody(Stream output, Stream input, long bodyLength) {
			// argument checks
			if (output == null) {
				throw new ArgumentNullException(nameof(output));
			}
			if (output.CanWrite == false) {
				throw new ArgumentException("It is not writable", nameof(output));
			}
			if (input == null) {
				throw new ArgumentNullException(nameof(input));
			}
			if (input.CanRead == false) {
				throw new ArgumentException("It is not readable", nameof(input));
			}

			// state checks
			// The bodyLength is not set because the body is not scanned
			// (instead, we are going to redirect the body).
			Debug.Assert(this.bodyLength == 0);

			// redirect the body
			// ToDo: attach stream to headerBuffer
			if (0 < bodyLength) {
				// normal body
				StoreBody(output, bodyLength);
			} else if (bodyLength == -1) {
				// chunked body
				StoreChunkedBody(output);
			} else if (bodyLength != 0) {
				Debug.Assert(bodyLength < -1);
				throw new ArgumentOutOfRangeException(nameof(bodyLength));
			}
			output.Flush();

			return;
		}

		#endregion


		#region overrides/overridables

		public override void ResetBuffer() {
			// reset this class level
			this.unflushedStart = 0;
			this.chunkingOutput = null;	// this object does not have its ownership
			this.bodyLength = 0;
			DisposableUtil.ClearDisposableObject(ref this.bodyStream);

			// reset the base class level
			base.ResetBuffer();
		}

		protected override byte[] UpdateMemoryBlock(byte[] currentMemoryBlock) {
			if (currentMemoryBlock != null) {
				Stream output = this.chunkingOutput;
				if (output != null && currentMemoryBlock != null) {
					// the case that it is handling chunked body

					// flush the bytes in the currentMemoryBlock to the chunkingOutputStream 
					int start = this.unflushedStart;
					int end = this.Limit;
					int count = end - start;
					if (0 < count) {
						chunkingOutput.Write(currentMemoryBlock, start, count);
						this.bodyLength += count;
					}
					this.unflushedStart = 0;
				}
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
	}
}
