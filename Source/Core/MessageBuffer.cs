using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;


namespace MAPE.Core {
	/// <summary>
	/// 
	/// </summary>
	/// <remarks>
	/// The instance of this class is not thread-safe.
	/// </remarks>
	public class MessageBuffer: IDisposable {
		#region types

		public struct Modification {
			#region data

			public readonly int Start;

			public readonly int End;

			public readonly Func<MessageBuffer, bool> Handler;

			#endregion


			#region creation and disposal

			public Modification(int start, int end, Func<MessageBuffer, bool> handler) {
				// argument checks
				if (start < 0) {
					throw new ArgumentOutOfRangeException(nameof(start));
				}
				if (end < start) {
					throw new ArgumentOutOfRangeException(nameof(end));
				}
				// handler can be null

				// initialize members
				this.Start = start;
				this.End = end;
				this.Handler = handler;

				return;
			}

			#endregion
		}

		#endregion


		#region constants

		public const int BodyStreamThreshold = 1024 * 1024;     // 1M


		// special byte values

		public const byte SP = 0x20;        // ' '

		public const byte HTAB = 0x09;      // '\t'

		public const byte CR = 0x0D;		// '\r'

		public const byte LF = 0x0A;		// '\n'

		public const byte Colon = 0x3A;     // ':'


		// misc

		public const string VersionPrefix = "HTTP/";

		public const string ChunkedTransferCoding = "chunked";

		#endregion


		#region data

		private static readonly char[] WS = new char[] { (char)SP, (char)HTAB };	// SP, HTAB


		// general

		private Stream input = null;

		private Stream output = null;

		private StringBuilder stockStringBuf = new StringBuilder();


		// header

		private List<byte[]> headerBuffer = new List<byte[]>();

		private byte[] currentMemoryBlock = null;

		private int currentMemoryBlockBase = 0;

		private int localLimit = 0;

		private int localIndex = 0;


		// body

		private long bodyLength = 0;

		private byte[] bodyBuffer = null;

		private Stream bodyStream = null;

		#endregion


		#region properties

		public bool IsStreamAttached {
			get {
				return this.input != null || this.output != null;
			}
		}

		public bool CanRead {
			get {
				return this.input != null;
			}
		}

		public bool CanWrite {
			get {
				return this.output != null;
			}
		}

		public int CurrentHeaderIndex {
			get {
				return this.currentMemoryBlockBase + this.localIndex;
			}
		}

		#endregion


		#region creation and disposal

		public MessageBuffer() {
		}

		public void Dispose() {
			// detach streams
			DetachStreams();

			// clear all resources
			this.stockStringBuf = null;
			this.headerBuffer = null;

			return;
		}

		/// <summary>
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <remarks>
		/// This object does not own the ownership of <paramref name="input"/> and <paramref name="output"/> .
		/// That is, this object does not Dispose them in its Detach() call.
		/// </remarks>
		public void AttachStreams(Stream input, Stream output) {
			// argument checks
			if (input == null && output == null) {
				throw new ArgumentException($"'{nameof(input)}' or '{nameof(output)}' must be non-null.");
			}

			// state checks
			if (this.IsStreamAttached) {
				throw new InvalidOperationException("This object is already attached streams.");
			}
			Debug.Assert(this.input == null && this.output == null);
			// the buffer state should be 'reset' state 
			Debug.Assert(this.currentMemoryBlockBase == 0 && this.localLimit == 0 && this.localIndex == 0);

			// set input and output
			this.input = input;
			this.output = output;

			return;
		}

		public void DetachStreams() {
			// state checks
			if (this.IsStreamAttached == false) {
				// nothing to do
				return;
			}

			// do not dispose input and output, just clear them
			// This object does not own the ownership of them.
			this.output = null;
			this.input = null;

			// reset the buffer state
			ResetBuffer();

			return;
		}

		#endregion


		#region methods - utilities

		public static Exception CreateBadRequestException() {
			// ToDo: Exception Type
			throw new Exception();
		}

		public static Version ParseVersion(string value) {
			// argument checks
			if (value == null) {
				throw new ArgumentNullException(nameof(value));
			}
			if (value.StartsWith(VersionPrefix) == false) {
				// invalid syntax
				throw CreateBadRequestException();
			}

			// parse HTTP-version
			// This parsing does not check strict syntax.
			// This is enough for our use.
			Version version;
			if (Version.TryParse(value.Substring(VersionPrefix.Length), out version) == false) {
				throw CreateBadRequestException();
			}

			return version;
		}

		public static int ParseStatusCode(string value) {
			// argument checks
			if (value == null) {
				throw new ArgumentNullException(nameof(value));
			}

			// parse status-code
			// This parsing does not check strict syntax.
			// This is enough for our use.
			int statusCode;
			if (int.TryParse(value, out statusCode) == false) {
				throw CreateBadRequestException();
			}

			return statusCode;
		}

		public static string TrimHeaderFieldValue(string fieldValue) {
			// argument checks
			if (fieldValue == null) {
				throw new ArgumentNullException(nameof(fieldValue));
			}

			return fieldValue.Trim(WS);
		}

		public static long ParseHeaderFieldValueAsLong(string fieldValue) {
			// argument checks
			if (fieldValue == null) {
				throw new ArgumentNullException(nameof(fieldValue));
			}
			fieldValue = TrimHeaderFieldValue(fieldValue);

			// parse the field value as long
			// This parsing does not check strict syntax.
			// This is enough for our use.
			long value;
			if (long.TryParse(fieldValue, out value) == false) {
				throw CreateBadRequestException();
			}

			return value;
		}

		public static bool IsChunkedSpecified(string decapitalizedFieldValue) {
			// argument checks
			if (decapitalizedFieldValue == null) {
				throw new ArgumentNullException(nameof(decapitalizedFieldValue));
			}
			decapitalizedFieldValue = TrimHeaderFieldValue(decapitalizedFieldValue);

			// check whether 'chunked' is specified at the last of the fieldValue
			// This parsing does not check strict syntax.
			// This is enough for our use.
			if (decapitalizedFieldValue.EndsWith(ChunkedTransferCoding)) {
				int prevIndex = decapitalizedFieldValue.Length - ChunkedTransferCoding.Length - 1;
				if (prevIndex < 0) {
					return true;
				}
				switch (decapitalizedFieldValue[prevIndex]) {
					case (char)SP:
					case (char)HTAB:
					case (char)Colon:
					case ',':
						return true;
				}
			}

			return false;
		}

		#endregion


		#region methods - read

		public void ResetBuffer() {
			// state checks
			List<byte[]> headerBuffer = this.headerBuffer;
			if (headerBuffer == null) {
				// nothing to do
				return;
			}

			// free resources for body buffering
			Stream stream = this.bodyStream;
			this.bodyStream = null;
			if (stream != null) {
				try {
					stream.Dispose();
				} catch {
					// continue
				}
			}

			byte[] memoryBlock = this.bodyBuffer;
			this.bodyBuffer = null;
			if (memoryBlock != null) {
				ComponentFactory.FreeMemoryBlock(memoryBlock);
				memoryBlock = null;
			}

			this.bodyLength = 0;

			// free resources for header buffering
			headerBuffer.ForEach(
				(block) => {
					try {
						ComponentFactory.FreeMemoryBlock(block);
					} catch {
						// continue
					}
				}
			);
			headerBuffer.Clear();

			this.currentMemoryBlock = null;
			this.currentMemoryBlockBase = 0;
			this.localLimit = 0;
			this.localIndex = 0;

			// misc
			Debug.Assert(this.stockStringBuf.Length == 0);

			return;
		}

		public string ReadStartLineItem(bool skipItem, bool decapitalize, bool lastItem) {
			string item;

			// read or skip the next item
			bool endOfLine;
			if (skipItem) {
				// skip the next item
				if (lastItem) {
					SkipHeaderToCRLF();
					endOfLine = true;
				} else {
					endOfLine = SkipHeaderTo(SP);
				}
				item = null;
			} else {
				// read the next item
				StringBuilder stringBuf = this.stockStringBuf;
				Debug.Assert(stringBuf.Length == 0);
				try {
					if (lastItem) {
						ReadHeaderASCIIToCRLF(stringBuf, decapitalize);
						endOfLine = true;
					} else {
						endOfLine = ReadHeaderASCIITo(SP, stringBuf, decapitalize);
					}
					item = stringBuf.ToString();
				} finally {
					stringBuf.Clear();
				}
			}

			// check status of the line
			if (endOfLine && lastItem == false) {
				// this item should not be the last one in the start line 
				throw CreateBadRequestException();
			}

			return item;
		}

		public byte ReadHeaderFieldNameFirstByte() {
			return ReadHeaderByte();
		}

		public string ReadHeaderFieldName(byte firstByte) {
			StringBuilder stringBuf = this.stockStringBuf;
			Debug.Assert(stringBuf.Length == 0);
			try {
				bool endOfLine = ReadHeaderASCIITo(Colon, stringBuf, decapitalize: true, firstByte: firstByte);
				if (endOfLine) {
					throw CreateBadRequestException();
				}
				return stringBuf.ToString();
			} finally {
				stringBuf.Clear();
			}
		}

		public string ReadHeaderFieldASCIIValue(bool decapitalize) {
			StringBuilder stringBuf = this.stockStringBuf;
			Debug.Assert(stringBuf.Length == 0);
			try {
				ReadHeaderASCIIToCRLF(stringBuf, decapitalize);
				return stringBuf.ToString();
			} finally {
				stringBuf.Clear();
			}
		}

		public bool SkipHeaderField(byte firstByte) {
			return SkipHeaderToCRLF(firstByte);
		}

		public bool SkipHeaderField() {
			return SkipHeaderToCRLF();
		}

		public void SkipBody(long contentLength) {
			// argument checks
			if (contentLength < 0) {
				// Note contentLength == -1 means that the body is chunked.
				// Use SkipChunkedBody() for chunked body.
				throw new ArgumentOutOfRangeException(nameof(contentLength));
			}

			// state checks
			Debug.Assert(this.CanRead);

			Func<byte[], int, int> read = (buf, offset) => {
				Debug.Assert(offset < buf.Length);
				int readCount = this.input.Read(buf, offset, buf.Length - offset);
				if (readCount <= 0) {
					// unexpected end of stream
					throw CreateBadRequestException();
				}
				offset += readCount;
				return offset;
			};

			// select media to store body depending on its size 
			byte[] bodyBuffer = null;
			Stream bodyStream = null;
			try {
				byte[] currentMemoryBlock = this.currentMemoryBlock;
				Debug.Assert(currentMemoryBlock != null);
				if (contentLength <= currentMemoryBlock.Length - this.localIndex) {
					// The body can be stored in the rest of header buffer.

					// read body into the header buffer
					int offset = this.localLimit;
					int limit = this.localIndex + (int)contentLength;
					while (offset < limit) {
						offset = read(currentMemoryBlock, offset);
					}
				} else {
					bodyBuffer = ComponentFactory.AllocMemoryBlock();
					if (contentLength <= bodyBuffer.Length) {
						// The body can be stored in a memory block.
						int limit = (int)contentLength;

						// copy the body bytes in the header buffer
						int offset = this.localLimit - this.localIndex;
						if (0 < offset) {
							Buffer.BlockCopy(currentMemoryBlock, this.localIndex, bodyBuffer, 0, offset);
						}

						// read body into the bodyBuffer 
						while (offset < limit) {
							offset = read(bodyBuffer, offset);
						}
					} else {
						// The body is stored in a stream.
						if (contentLength <= BodyStreamThreshold) {
							// use memory stream
							Debug.Assert(contentLength <= int.MaxValue);
							bodyStream = new MemoryStream((int)contentLength);
						} else {
							// user temp file stream
							bodyStream = CreateTempFileStream();
						}

						// write the body bytes in the header buffer into the stream
						long amount = 0;
						int count = this.localLimit - this.localIndex;
						if (0 < count) {
							bodyStream.Write(currentMemoryBlock, this.localIndex, count);
							amount += count;
						}

						// write body into the stream 
						while (amount < contentLength) {
							count = read(bodyBuffer, 0);
							bodyStream.Write(bodyBuffer, 0, count);
							amount += count;
						}
					}
				}

				this.bodyLength = contentLength;
				this.bodyBuffer = bodyBuffer;
				this.bodyStream = bodyStream;
			} catch {
				this.bodyLength = 0;
				if (bodyStream != null) {
					bodyStream.Dispose();
				}
				if (bodyBuffer != null) {
					ComponentFactory.FreeMemoryBlock(bodyBuffer);
				}
				throw;
			}

			return;
		}

		public void SkipChunkedBody() {
			throw new NotImplementedException();
		}

		#endregion


		#region methods - write

		public void WriteHeader(IEnumerable<Modification> modifications) {
			throw new NotImplementedException();
		}

		public void WriteHeader() {
			// state checks
			Debug.Assert(this.CanWrite);
			Stream output = this.output;
			Debug.Assert(output != null);

			// write header part simply
			byte[] currentMemoryBlock = this.currentMemoryBlock;
			Debug.Assert(currentMemoryBlock != null);
			foreach (byte[] memoryBlock in this.headerBuffer) {
				if (memoryBlock != currentMemoryBlock) {
					output.Write(memoryBlock, 0, memoryBlock.Length);
				} else {
					// the last memory block
					output.Write(memoryBlock, 0, this.localIndex);
				}
			}

			return;
		}

		public void WriteBody() {
			// state checks
			Debug.Assert(this.CanWrite);
			Stream output = this.output;
			Debug.Assert(output != null);

			// Note that bodyLength == -1 means the chunked body
			if (this.bodyLength == 0) {
				// no body
				output.Flush();
				return;
			}
			Debug.Assert(this.bodyLength != -1 || this.bodyStream != null);

			// write the body
			// Note the media where the body is stored depends on its size.  
			if (this.bodyStream != null) {
				// body is stored in the stream (large body or chunked body)
				this.bodyStream.Seek(0, SeekOrigin.Begin);
				this.bodyStream.CopyTo(output);
			} else {
				Debug.Assert(0 <= this.bodyLength && this.bodyLength <= int.MaxValue);
				int bodyLengthInInt = (int)this.bodyLength;

				if (this.bodyBuffer != null) {
					// body is stored in the buffer (small body)
					output.Write(this.bodyBuffer, 0, bodyLengthInInt);
				} else {
					// body is stored in the rest of the header buffer (very small body)
					output.Write(this.currentMemoryBlock, this.localIndex, bodyLengthInInt);
				}
			}
			output.Flush();

			return;
		}

		#endregion


		#region privates - general

		private void FillHeaderBuffer() {
			// state checks
			Debug.Assert(this.input != null);
			Debug.Assert(this.headerBuffer != null);
			Debug.Assert(this.localIndex == this.localLimit);

			// alloc a new memory block if the current block is full
			byte[] memoryBlock = this.currentMemoryBlock;
			Debug.Assert(memoryBlock != null || this.headerBuffer.Count == 0);
			if (memoryBlock == null || memoryBlock.Length <= this.localLimit) {
				// calculate the next lineBase
				int nextBase = (memoryBlock == null) ? 0 : this.currentMemoryBlockBase + memoryBlock.Length;

				// allocate a new memory block
				memoryBlock = ComponentFactory.AllocMemoryBlock();
				this.headerBuffer.Add(memoryBlock);

				// update the header buffer state
				this.currentMemoryBlock = memoryBlock;
				this.currentMemoryBlockBase = nextBase;
				this.localLimit = 0;
				this.localIndex = 0;
			}

			// read bytes from the input
			Debug.Assert(memoryBlock != null && this.localLimit < memoryBlock.Length);
			int readCount = this.input.Read(memoryBlock, this.localLimit, memoryBlock.Length - this.localLimit);
			if (readCount <= 0) {
				// unexpected end of stream
				throw CreateBadRequestException();
			}

			// update the data limit
			this.localLimit += readCount;

			return;
		}

		private static void AppendByteAsASCII(StringBuilder stringBuf, byte b) {
			// argument checks
			Debug.Assert(stringBuf != null);

			stringBuf.Append((char)b);
		}

		private static void AppendByteAsDecapitalizedASCII(StringBuilder stringBuf, byte b) {
			// argument checks
			if (0x41 <= b && b <= 0x5A) {
				// decapitalize upper-case char
				b += 0x20;
			}

			AppendByteAsASCII(stringBuf, b);
		}

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


		#region privates - read

		private byte ReadHeaderByte() {
			// fill new bytes if no more unread byte
			if (this.localLimit <= this.localIndex) {
				FillHeaderBuffer();
				// Note that this.currentMemoryBlock, this.localIndex and this.localLimit may be changed.
			}
			Debug.Assert(this.localIndex < this.localLimit);

			// return the next byte
			return this.currentMemoryBlock[this.localIndex++];
		}

		private bool SkipHeaderToCRLF(byte firstByte) {
			bool emptyLine = true;
			byte b = firstByte;
			do {
				if (b == CR) {
					do {
						b = ReadHeaderByte();
						if (b == LF) {
							// CRLF
							return emptyLine;
						}
						emptyLine = false;
					} while (b == CR);
				}
				b = ReadHeaderByte();
				emptyLine = false;
			} while (true);
		}

		private bool SkipHeaderToCRLF() {
			return SkipHeaderToCRLF(ReadHeaderByte());
		}

		private bool SkipHeaderTo(byte terminator) {
			// argument checks
			Debug.Assert(terminator != CR);
			Debug.Assert(terminator != LF);

			byte b;
			do {
				b = ReadHeaderByte();
				if (b == CR) {
					do {
						b = ReadHeaderByte();
						if (b == LF) {
							// CRLF
							return true;
						}
					} while (b == CR);
				}
				if (b == terminator) {
					// not CRLF
					return false;
				}
			} while (true);
		}

		private void ReadHeaderASCIIToCRLF(StringBuilder stringBuf, bool decapitalize) {
			// argument checks
			Debug.Assert(stringBuf != null);

			// determine the way to append byte
			Action<StringBuilder, byte> appendByte;
			if (decapitalize) {
				appendByte = AppendByteAsDecapitalizedASCII;
			} else {
				appendByte = AppendByteAsASCII;
			}

			// read header bytes to CRLF as ASCII chars
			byte b;
			do {
				b = ReadHeaderByte();
				if (b == CR) {
					do {
						b = ReadHeaderByte();
						if (b == LF) {
							// CRLF
							return;
						}
						appendByte(stringBuf, CR);
					} while (b == CR);
				}
				appendByte(stringBuf, b);
			} while (true);
		}

		private bool ReadHeaderASCIITo(byte terminator, StringBuilder stringBuf, bool decapitalize, byte firstByte) {
			// argument checks
			Debug.Assert(terminator != CR);
			Debug.Assert(terminator != LF);
			Debug.Assert(stringBuf != null);

			// determine the way to append byte
			Action<StringBuilder, byte> append;
			if (decapitalize) {
				append = AppendByteAsDecapitalizedASCII;
			} else {
				append = AppendByteAsASCII;
			}

			// read header bytes to the terminator as ASCII chars
			byte b = firstByte;
			do {
				if (b == CR) {
					do {
						b = ReadHeaderByte();
						if (b == LF) {
							// CRLF
							return true;
						}
						append(stringBuf, CR);
					} while (b == CR);
				}
				if (b == terminator) {
					// not CRLF
					return false;
				}
				append(stringBuf, b);
				b = ReadHeaderByte();
			} while (true);
		}

		private bool ReadHeaderASCIITo(byte terminator, StringBuilder stringBuf, bool decapitalize) {
			return ReadHeaderASCIITo(terminator, stringBuf, decapitalize, ReadHeaderByte());
		}

		#endregion
	}
}
