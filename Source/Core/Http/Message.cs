using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MAPE.ComponentBase;


namespace MAPE.Http {
	public abstract class Message: IDisposable, ICacheableObject<IMessageIO> {
		#region data

		private readonly HeaderBuffer headerBuffer;

		private readonly BodyBuffer bodyBuffer;

		public MessageReadingState ReadingState { get; private set; }

		private List<MessageBuffer.Modification> modifications;

		public Version Version {
			get;
			protected set;
		}

		public long ContentLength {
			get;
			protected set;
		}

		public Span EndOfHeaderFields {
			get;
			protected set;
		}

		#endregion


		#region properties

		public bool CanRead {
			get {
				return this.headerBuffer.CanRead;
			}
		}

		public bool MessageRead {
			get {
				return this.ReadingState == MessageReadingState.Body;
			}
		}

		protected IMessageIO IO {
			get {
				return this.headerBuffer.IO;
			}
		}

		protected IReadOnlyList<MessageBuffer.Modification> Modifications {
			get {
				return this.modifications;
			}
		}

		#endregion


		#region creation and disposal

		protected Message() {
			// initialize members
			this.headerBuffer = new HeaderBuffer();
			this.bodyBuffer = new BodyBuffer(this.headerBuffer);
			this.modifications = new List<MessageBuffer.Modification>();
			ResetThisClassLevelMessageProperties();
			this.ReadingState = MessageReadingState.Error;

			return;
		}

		public void Dispose() {
			DetachIO();
			this.Version = null;
			this.modifications.Clear();
			this.modifications = null;
			this.bodyBuffer.Dispose();
			this.headerBuffer.Dispose();

			return;
		}

		public void AttachIO(IMessageIO io) {
			// argument checks
			if (io == null) {
				throw new ArgumentNullException(nameof(io));
			}

			// state checks
			if (this.headerBuffer.IO != null) {
				throw new InvalidOperationException("Another IMessageIO object is being attached now.");
			}

			// attach MessageIO
			this.headerBuffer.IO = io;
			this.ReadingState = MessageReadingState.None;

			return;
		}

		public void DetachIO() {
			// detach the MessageIO
			Reset();
			this.ReadingState = MessageReadingState.Error;
			this.headerBuffer.IO = null;

			return;
		}

		#endregion


		#region ICacheableObject<ICommunicationIO>

		public void OnCaching() {
			// reset instance
			DetachIO();
			Debug.Assert(this.ReadingState == MessageReadingState.Error);

			return;
		}

		public void OnDecached(IMessageIO io) {
			// argument checks
			if (io == null) {
				throw new ArgumentNullException(nameof(io));
			}

			// state checks
			Debug.Assert(this.ReadingState == MessageReadingState.Error);

			// prepare for decaching
			AttachIO(io);

			return;
		}

		#endregion


		#region methods

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		/// <remarks>
		/// The public version of Read() is provided by derived class, Request class and Response class,
		/// utilizing this implementation.
		/// </remarks>
		protected bool Read() {
			// read a message
			if (ReadHeader()) {
				ReadBody();
			}

			return this.ReadingState == MessageReadingState.Body;
		}

		protected bool ReadHeader() {
			// read header part from the input
			bool read = false;
			try {
				HeaderBuffer headerBuffer = this.headerBuffer;

				// state checks
				switch (this.ReadingState) {
					case MessageReadingState.Error:
						throw CreateNoIOException();
					case MessageReadingState.None:
						break;
					default:
						Reset();
						break;
				}
				Debug.Assert(this.headerBuffer.CanRead);

				// read start line
				ScanStartLine(headerBuffer);

				// read header fields
				bool emptyLine;
				do {
					emptyLine = ScanHeaderField(headerBuffer);
				} while (emptyLine == false);

				// update state
				int endOfHeaderOffset = headerBuffer.CurrentOffset - 2;  // subtract empty line bytes
				this.EndOfHeaderFields = new Span(endOfHeaderOffset, endOfHeaderOffset);
				this.ReadingState = MessageReadingState.Header;
				read = true;
			} catch (EndOfStreamException) {
				// no data from input
				// Note that incomplete data results an exception other than EndOfStreamException.
				Debug.Assert(this.ReadingState == MessageReadingState.None);
				Debug.Assert(read == false);
				// continue
			} catch {
				this.ReadingState = MessageReadingState.Error;
				throw;
			}

			return read;
		}

		protected void ReadBody() {
			// state checks
			if (this.ReadingState != MessageReadingState.Header) {
				throw new InvalidOperationException("The current position is not the end of the header.");
			}

			// read body part from the input
			// Note that the bodyBuffer reads bytes through the headerBuffer.
			try {
				// state checks
				Debug.Assert(this.headerBuffer.CanRead);
				Debug.Assert(this.bodyBuffer.CanRead);

				// read body
				ScanBody(this.bodyBuffer);
				this.ReadingState = MessageReadingState.Body;
			} catch {
				this.ReadingState = MessageReadingState.Error;
				throw;
			}

			return;
		}

		public void SkipBody() {
			// state checks
			if (this.ReadingState != MessageReadingState.Header) {
				throw new InvalidOperationException("The current position is not the end of the header.");
			}

			// redirect the body to Null stream
			RedirectBody(Stream.Null, this.bodyBuffer);

			// update state
			this.ReadingState = MessageReadingState.BodyRedirected;

			return;
		}

		public void Write(bool suppressModification = false) {
			// state checks
			if (this.ReadingState != MessageReadingState.Body) {
				throw new InvalidOperationException("The body part is not read.");
			}
			Stream output = EnsureOutput();
			Debug.Assert(output != null);

			// write the message
			IEnumerable<MessageBuffer.Modification> modifications = (suppressModification == false && 0 < this.modifications.Count) ? this.modifications : null;
			WriteHeader(output, this.headerBuffer, modifications);
			WriteBody(output, this.bodyBuffer);

			return;
		}

		public void Redirect(bool suppressModification = false) {
			// state checks
			if (this.ReadingState != MessageReadingState.Header) {
				throw new InvalidOperationException("The message is not read.");
			}
			Stream output = EnsureOutput();
			Debug.Assert(output != null);

			// write/redirect a message
			IEnumerable<MessageBuffer.Modification> modifications = (suppressModification == false && 0 < this.modifications.Count) ? this.modifications : null;
			WriteHeader(output, this.headerBuffer, modifications);
			RedirectBody(output, this.bodyBuffer);

			// update state
			this.ReadingState = MessageReadingState.BodyRedirected;

			return;
		}

		public void ClearModifications() {
			this.modifications.Clear();
		}

		public void AddModification(Span span, Func<Modifier, bool> handler) {
			// argument checks
			Debug.Assert(0 <= span.Start);
			Debug.Assert(span.Start <= span.End);
			// handler can be null

			// state checks
			List<MessageBuffer.Modification> modifications = this.modifications;
			Debug.Assert(modifications != null);

			// find the insertion point
			int index = modifications.Count;
			for (int i = 0; i < modifications.Count; ++i) {
				MessageBuffer.Modification modification = modifications[i];
				if (span.End < modification.Start) {
					index = i;
					break;
				} else if (span.End == modification.Start) {
					if (0 < modification.Length || 0 < span.Length) {
						index = i;
						break;
					}
					// continue
					// keep order of the inserting (0-length) modifications on the same point 
				} else if (span.Start < modification.End) {
					// overlapped
					throw new ArgumentException("It conflicts with an existing span.", nameof(span));
				}
			}

			// insert a modification
			modifications.Insert(index, new MessageBuffer.Modification(span, handler));

			return;
		}


		protected static InvalidOperationException CreateNoIOException() {
			return new InvalidOperationException("No stream to read or write is attached.");
		}

		protected Stream EnsureOutput() {
			Stream output = this.IO?.Output;
			if (output == null) {
				throw CreateNoIOException();
			}

			return output;
		}

		#endregion


		#region overridables

		protected virtual void Reset() {
			// reset message properties
			ResetThisClassLevelMessageProperties();

			// reset buffers
			this.bodyBuffer.ResetBuffer();
			this.headerBuffer.ResetBuffer();

			return;
		}

		protected abstract void ScanStartLine(HeaderBuffer headerBuffer);

		protected virtual bool ScanHeaderField(HeaderBuffer headerBuffer) {
			// argument checks
			Debug.Assert(headerBuffer != null);

			// read the first byte
			bool emptyLine;
			Func<byte, bool> hasInterest = (b) => {
				char c = Char.ToLower((char)b);
				return IsInterestingHeaderFieldFirstChar(c);
			};

			byte firstByte = headerBuffer.ReadFieldNameFirstByte();
			if (firstByte == MessageBuffer.CR || hasInterest(firstByte) == false) {
				// no interest, just skip this line
				emptyLine = headerBuffer.SkipField(firstByte);
			} else {
				// scan this field
				int startOffset = headerBuffer.CurrentOffset - 1;	// Note we have already read one byte
				string decapitalizedFieldName = headerBuffer.ReadFieldName(firstByte);
				ScanHeaderFieldValue(headerBuffer, decapitalizedFieldName, startOffset);
				emptyLine = false;
			}

			return emptyLine;
		}

		protected virtual bool IsInterestingHeaderFieldFirstChar(char decapitalizedFirstChar) {
			switch (decapitalizedFirstChar) {
				case 'c':   // possibly "content-length"
				case 't':   // possibly "transfer-encoding"
					return true;
				default:
					return false;
			}
		}

		protected virtual void ScanHeaderFieldValue(HeaderBuffer headerBuffer, string decapitalizedFieldName, int startOffset) {
			// argument checks
			Debug.Assert(headerBuffer != null);

			string value;
			switch (decapitalizedFieldName) {
				case "content-length":
					value = headerBuffer.ReadFieldASCIIValue(decapitalize: false);
					this.ContentLength = HeaderBuffer.ParseHeaderFieldValueAsLong(value);
					break;
				case "transfer-encoding":
					value = headerBuffer.ReadFieldASCIIValue(decapitalize: true);
					if (HeaderBuffer.IsChunkedSpecified(value) == false) {
						throw MessageBuffer.CreateBadRequestException();
					}
					this.ContentLength = -1;	// -1 means 'chunked'
					break;
				default:
					// just skip
					headerBuffer.SkipField();
					break;
			}
		}

		protected virtual void ScanBody(BodyBuffer bodyBuffer) {
			// argument checks
			Debug.Assert(bodyBuffer != null);

			// scan body
			if (this.ContentLength == -1) {
				// chunked body
				bodyBuffer.SkipChunkedBody();
			} else {
				// simple body
				// Call bodyBuffer.SkipBody() even if bodyLength is 0 to process prefetched bytes.
				bodyBuffer.SkipBody(this.ContentLength);
			}

			return;
		}

		protected virtual void WriteHeader(Stream output, HeaderBuffer headerBuffer, IEnumerable<MessageBuffer.Modification> modifications) {
			// argument checks
			Debug.Assert(output != null);
			Debug.Assert(headerBuffer != null);
			// modifications can be null

			// write message header
			headerBuffer.WriteHeader(output, modifications);
		}

		protected virtual void WriteBody(Stream output, BodyBuffer bodyBuffer) {
			// argument checks
			Debug.Assert(output != null);
			Debug.Assert(bodyBuffer != null);

			// write message body
			bodyBuffer.WriteBody(output);
		}

		protected virtual void RedirectBody(Stream output, BodyBuffer bodyBuffer) {
			// argument checks
			Debug.Assert(output != null);
			Debug.Assert(bodyBuffer != null);

			// write message body
			bodyBuffer.RedirectBody(output, this.ContentLength);
		}

		#endregion


		#region privates

		private void ResetThisClassLevelMessageProperties() {
			// reset message properties of this class level
			this.EndOfHeaderFields = Span.ZeroToZero;
			this.ContentLength = 0;
			this.Version = null;
			this.modifications.Clear();
			this.ReadingState = MessageReadingState.None;

			return;
		}

		#endregion
	}
}
