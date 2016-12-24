using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;


namespace MAPE.Http {
	public abstract class MessageBuffer: IDisposable {
		#region types

		public struct Modifier {
			#region data

			public readonly Stream output;

			#endregion


			#region creation and disposal

			public Modifier(Stream output) {
				// argument checks
				Debug.Assert(output != null);

				// initialize members
				this.output = output;

				return;
			}

			#endregion


			#region methods

			public void Write(byte[] data, bool appendCRLF = false) {
				// write data
				if (data != null && 0 < data.Length) {
					this.output.Write(data, 0, data.Length);
				}
				if (appendCRLF) {
					this.output.WriteByte(CR);
					this.output.WriteByte(LF);
				}

				return;
			}

			public void WriteASCIIString(string str, bool appendCRLF = false) {
				// write data
				byte[] data;
				if (string.IsNullOrEmpty(str)) {
					data = null;
				} else {
					data = Encoding.ASCII.GetBytes(str);
				}
				Write(data, appendCRLF);

				return;
			}

			#endregion
		}

		public struct Span {
			#region data

			public static readonly Span ZeroToZero = new Span(0, 0);

			public readonly int Start;

			public readonly int End;

			#endregion


			#region properties

			public bool IsZeroToZero {
				get {
					return this.Start == 0 && this.End == 0;
				}
			}

			#endregion


			#region creation and disposal

			public Span(int start, int end) {
				// argument checks
				if (start < 0) {
					throw new ArgumentOutOfRangeException(nameof(start));
				}
				if (end < start) {
					throw new ArgumentOutOfRangeException(nameof(end));
				}

				// initialize members
				this.Start = start;
				this.End = end;

				return;
			}

			public Span(Span src) {
				// initialize members
				this.Start = src.Start;
				this.End = src.End;

				return;
			}

			#endregion
		}

		public struct Modification {
			#region data

			public readonly Span Span;

			public readonly Func<Modifier, bool> Handler;

			#endregion


			#region properties

			public int Start {
				get {
					return this.Span.Start;
				}
			}

			public int End {
				get {
					return this.Span.End;
				}
			}

			public int Length {
				get {
					return this.Span.End - this.Span.Start;
				}
			}

			#endregion


			#region creation and disposal

			public Modification(int start, int end, Func<Modifier, bool> handler) {
				// argument checks
				Debug.Assert(0 <= start);
				Debug.Assert(start <= end);
				// handler can be null

				// initialize members
				this.Span = new Span(start, end);
				this.Handler = handler;

				return;
			}

			public Modification(Span span, Func<Modifier, bool> handler) {
				// argument checks
				// handler can be null

				// initialize members
				this.Span = span;
				this.Handler = handler;

				return;
			}

			#endregion
		}

		#endregion


		#region constants

		// special byte values

		public const byte SP = 0x20;        // ' '

		public const byte HTAB = 0x09;      // '\t'

		public const byte CR = 0x0D;        // '\r'

		public const byte LF = 0x0A;        // '\n'

		public const byte Colon = 0x3A;     // ':'


		// misc

		public const string VersionPrefix = "HTTP/";

		public const string ChunkedTransferCoding = "chunked";

		#endregion


		#region data

		private byte[] memoryBlock;

		private int limit;

		private int next;

		#endregion


		#region properties

		public int Limit {
			get {
				return this.limit;
			}
		}

		public int Next {
			get {
				return this.next;
			}
		}

		public int Margin {
			get {
				byte[] memoryBlock = this.memoryBlock;
				if (memoryBlock == null) {
					throw new InvalidOperationException();
				}

				return memoryBlock.Length - this.limit;
			}
		}

		protected byte[] MemoryBlock {
			get {
				return this.memoryBlock;
			}
		}

		#endregion


		#region creation and disposal

		public MessageBuffer() {
			// initialize members
			this.memoryBlock = null;
			this.limit = 0;
			this.next = 0;

			return;
		}

		public virtual void Dispose() {
			// state checks
			// Its resources have been cleared at this point.
			// Because derived class level implementations clear their resources
			// before they call this class level implementation,
			// and it includes indirect ResetBuffer() call. 
			// See the derived class implementations (HeaderBuffer.Dispose() and BodyBuffer.Dispose()).
			Debug.Assert(this.next == 0);
			Debug.Assert(this.limit == 0);
			Debug.Assert(this.memoryBlock == null);
		}

		#endregion


		#region methods

		public static HttpException CreateBadRequestException() {
			return new HttpException(HttpStatusCode.BadRequest);
		}

		public static bool IsValidModifications(IEnumerable<Modification> modifications) {
			if (modifications == null) {
				// null is valid
				return true;
			}

			// Modifications must be sorted and their spans must not be overlapped.
			int last = 0;
			foreach (Modification modification in modifications) {
				// Is its span overlapped with the previous span?
				if (modification.Start < last) {
					return false;
				}

				// from definition of Modification structure, Start <= End
				Debug.Assert(modification.Start <= modification.End);

				last = modification.End;
			}

			return true;
		}


		protected byte[] EnsureMemoryBlockAllocated() {
			byte[] memoryBlock = this.memoryBlock;
			if (memoryBlock == null) {
				memoryBlock = UpdateMemoryBlock(null);
				this.memoryBlock = memoryBlock;
				Debug.Assert(this.limit == 0);
				Debug.Assert(this.next == 0);
			}

			return memoryBlock;
		}

		protected void FillBuffer(int count) {
			// state checks
			byte[] memoryBlock = EnsureMemoryBlockAllocated();

			int offset = this.limit;
			if (count < 0 || memoryBlock.Length - offset < count) {
				throw new ArgumentOutOfRangeException(nameof(count));
			}
			int newLimit = this.limit + count;

			// read bytes from the input until the requested count of bytes are read 		
			while (offset < newLimit) {
				int readCount = ReadBytes(memoryBlock, offset, newLimit - offset);
				Debug.Assert(0 < readCount);    // ReadBytes() throws an exception at end of stream 
				offset += readCount;
			}
			Debug.Assert(offset == newLimit);

			// update its state
			this.limit = newLimit;

			return;
		}

		protected byte ReadNextByte() {
			// fill bytes if no more unread byte
			if (this.limit <= this.next) {
				UpdateBuffer();
				// Note that this.memoryBlock, this.limit and this.next may be changed.
			}
			Debug.Assert(this.next < this.limit);

			// return the next byte
			return this.memoryBlock[this.next++];
		}

		protected bool SkipToCRLF(byte firstByte) {
			bool emptyLine = true;
			byte b = firstByte;
			do {
				if (b == CR) {
					do {
						b = ReadNextByte();
						if (b == LF) {
							// CRLF
							return emptyLine;
						}
						emptyLine = false;
					} while (b == CR);
				}
				b = ReadNextByte();
				emptyLine = false;
			} while (true);
		}

		protected bool SkipToCRLF() {
			return SkipToCRLF(ReadNextByte());
		}

		protected bool SkipTo(byte terminator) {
			// argument checks
			Debug.Assert(terminator != CR);
			Debug.Assert(terminator != LF);

			byte b;
			do {
				b = ReadNextByte();
				if (b == CR) {
					do {
						b = ReadNextByte();
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

		protected void ReadASCIIToCRLF(StringBuilder stringBuf, bool decapitalize) {
			// argument checks
			Debug.Assert(stringBuf != null);

			// determine the way to append byte
			Action<StringBuilder, byte> appendByte;
			if (decapitalize) {
				appendByte = AppendByteAsDecapitalizedASCII;
			} else {
				appendByte = AppendByteAsASCII;
			}

			// read bytes to CRLF as ASCII chars
			byte b;
			do {
				b = ReadNextByte();
				if (b == CR) {
					do {
						b = ReadNextByte();
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

		protected bool ReadASCIITo(byte terminator, StringBuilder stringBuf, bool decapitalize, byte firstByte) {
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

			// read bytes to the terminator as ASCII chars
			byte b = firstByte;
			do {
				if (b == CR) {
					do {
						b = ReadNextByte();
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
				b = ReadNextByte();
			} while (true);
		}

		protected bool ReadASCIITo(byte terminator, StringBuilder stringBuf, bool decapitalize) {
			return ReadASCIITo(terminator, stringBuf, decapitalize, ReadNextByte());
		}

		protected void CopyFrom(MessageBuffer source, int offset, int count) {
			// argument checks
			Debug.Assert(source != null);
			Debug.Assert(0 <= offset && offset <= source.limit);
			Debug.Assert(0 <= count && count <= source.limit - offset);
			byte[] destMemoryBlock = this.memoryBlock;
			Debug.Assert(destMemoryBlock != null);
			Debug.Assert(count <= destMemoryBlock.Length - this.limit);

			// copy bytes in the source buffer to this buffer
			if (0 < count) {
				byte[] sourceMemoryBlock = source.memoryBlock;
				if (sourceMemoryBlock == null) {
					throw new InvalidOperationException();
				}

				// copy bytes
				System.Buffer.BlockCopy(sourceMemoryBlock, offset, destMemoryBlock, this.limit, count);

				// update its state
				this.limit += count;
			}

			return;
		}

		protected void WriteTo(Stream output, int offset, int count) {
			// argument checks
			Debug.Assert(output != null);
			Debug.Assert(0 <= offset && offset <= this.limit);
			Debug.Assert(0 <= count && count <= this.limit - offset);

			// copy bytes in the source buffer to this buffer
			if (0 < count) {
				Debug.Assert(this.memoryBlock != null);
				output.Write(this.memoryBlock, offset, count);
			}

			return;
		}

		#endregion


		#region methods - accessibility bridges

		/// <summary>
		/// The bridge to call ReadBytes() on HeaderBuffer object from BodyBuffer object
		/// bypassing accessibility control.
		/// See BodyBuffer.ReadBytes().
		/// </summary>
		/// <param name="target"></param>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		protected static int ReadBytes(MessageBuffer target, byte[] buffer, int offset, int count) {
			// argument checks
			Debug.Assert(target != null);

			return target.ReadBytes(buffer, offset, count);
		}

		protected static void FillBuffer(MessageBuffer target, int count) {
			// argument checks
			Debug.Assert(target != null);

			target.FillBuffer(count);
		}

		protected static void WriteTo(MessageBuffer target, Stream output, int offset, int count) {
			// argument checks
			Debug.Assert(target != null);

			target.WriteTo(output, offset, count);
		}

		#endregion


		#region overridables

		public virtual void ResetBuffer() {
			// reset its buffer state
			this.next = 0;
			this.limit = 0;
			byte[] temp = this.memoryBlock;
			this.memoryBlock = null;
			if (temp != null) {
				ReleaseMemoryBlockOnResetBuffer(temp);
			}

			return;
		}

		protected virtual byte[] UpdateMemoryBlock(byte[] currentMemoryBlock) {
			// by default, reuse one memory block
			return (currentMemoryBlock != null) ? currentMemoryBlock : ComponentFactory.AllocMemoryBlock();
		}

		protected virtual void ReleaseMemoryBlockOnResetBuffer(byte[] memoryBlock) {
			// by default, release the 'only-one' memory block
			ComponentFactory.FreeMemoryBlock(memoryBlock);
		}

		protected abstract int ReadBytes(byte[] buffer, int offset, int count);

		#endregion


		#region privates

		private void UpdateBuffer() {
			// state checks
			Debug.Assert(this.next == this.limit);

			// update memory block if the current block is full
			byte[] memoryBlock = this.memoryBlock;
			if (memoryBlock == null || memoryBlock.Length <= this.limit) {
				// update memory block (it may be replaced)
				memoryBlock = UpdateMemoryBlock(memoryBlock);
				this.memoryBlock = memoryBlock;

				// update the state
				this.limit = 0;
				this.next = 0;
			}

			// read bytes from the input into the memory block
			Debug.Assert(memoryBlock != null && this.limit < memoryBlock.Length);
			int readCount = ReadBytes(memoryBlock, this.limit, memoryBlock.Length - this.limit);
			Debug.Assert(0 < readCount);	// ReadBytes() throws an exception on end of stream 

			// update the data limit
			this.limit += readCount;

			return;
		}

		private static void AppendByteAsASCII(StringBuilder stringBuf, byte b) {
			// argument checks
			Debug.Assert(stringBuf != null);

			stringBuf.Append((char)b);
		}

		private static void AppendByteAsDecapitalizedASCII(StringBuilder stringBuf, byte b) {
			// argument checks
			// decapitalize upper-case char
			if (0x41 <= b && b <= 0x5A) {
				b += 0x20;
			}

			AppendByteAsASCII(stringBuf, b);
		}

		#endregion
	}
}
