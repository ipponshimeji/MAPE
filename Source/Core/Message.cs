using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;


namespace MAPE.Core {
	public abstract class Message: IDisposable {
		#region data

		private MessageBuffer messageBuffer;


		public Version Version {
			get;
			protected set;
		}

		public long ContentLength {
			get;
			protected set;
		}

		public MessageBuffer.Span EndOfHeaderFields {
			get;
			protected set;
		}

		#endregion


		#region properties

		public bool IsStreamAttached {
			get {
				return this.messageBuffer != null && this.messageBuffer.IsStreamAttached;
			}
		}

		#endregion


		#region creation and disposal

		public Message() {
			// initialize members
			this.messageBuffer = new MessageBuffer();
			ResetThisClassLevelMessageProperties();

			return;
		}

		public void Dispose() {
			this.messageBuffer.Dispose();

			return;
		}

		#endregion


		#region methods - lifecycle

		/// <summary>
		/// 
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <remarks>
		/// This object does not own the ownership of <paramref name="input"/> and <paramref name="output"/> .
		/// That is, this object does not Dispose them in its Detach() call.
		/// </remarks>
		public void AttachStreams(Stream input, Stream output) {
			// argument checks
			Debug.Assert(input != null || output != null);

			// attach input and output
			this.messageBuffer.AttachStreams(input, output);
			Debug.Assert(this.ContentLength == 0);

			return;
		}

		public void DetachStreams() {
			// reset message properties read from input
			ResetMessageProperties();

			// detach input and output
			this.messageBuffer.DetachStreams();

			return;
		}

		#endregion


		#region methods - public

		public bool Read() {
			bool read = false;
			try {
				// state checks
				MessageBuffer messageBuffer = this.messageBuffer;
				Debug.Assert(messageBuffer.CanRead);

				// clear current contents
				ResetMessageProperties();
				messageBuffer.ResetBuffer();

				// read a message

				// start line
				ScanStartLine(messageBuffer);

				// header fields
				bool emptyLine;
				do {
					emptyLine = ScanHeaderField(messageBuffer);
				} while (emptyLine == false);
				int endOfHeaderIndex = messageBuffer.CurrentHeaderIndex - 2;    // subtract empty line bytes
				this.EndOfHeaderFields = new MessageBuffer.Span(endOfHeaderIndex, endOfHeaderIndex);

				// body
				ScanBody(messageBuffer);

				// result
				read = true;	// completed
			} catch (EndOfStreamException) {
				Debug.Assert(read == false);
				// continue
			}

			return read;
		}

		public void Write(IEnumerable<MessageBuffer.Modification> modifications = null) {
			// argument checks
			// modifications can be null

			// state checks
			MessageBuffer messageBuffer = this.messageBuffer;
			Debug.Assert(messageBuffer.CanWrite);

			// write a message
			WriteHeader(messageBuffer, modifications);
			WriteBody(messageBuffer);

			return;
		}

		#endregion


		#region overridables

		protected virtual void ResetMessageProperties() {
			// reset message properties
			ResetThisClassLevelMessageProperties();

			return;
		}

		protected abstract void ScanStartLine(MessageBuffer messageBuffer);

		protected virtual bool ScanHeaderField(MessageBuffer messageBuffer) {
			// argument checks
			Debug.Assert(messageBuffer != null);

			// read the first byte
			bool emptyLine;
			Func<byte, bool> hasInterest = (b) => {
				char c = Char.ToLower((char)b);
				return IsInterestingHeaderFieldFirstChar(c);
			};

			byte firstByte = messageBuffer.ReadHeaderFieldNameFirstByte();
			if (firstByte == MessageBuffer.CR || hasInterest(firstByte) == false) {
				// no interest, just skip this line
				emptyLine = messageBuffer.SkipHeaderField(firstByte);
			} else {
				// scan this field
				int startIndex = messageBuffer.CurrentHeaderIndex - 1;	// Note we have already read one byte
				string decapitalizedFieldName = messageBuffer.ReadHeaderFieldName(firstByte);
				ScanHeaderFieldValue(messageBuffer, decapitalizedFieldName, startIndex);
				emptyLine = false;
			}

			return emptyLine;
		}

		protected virtual bool IsInterestingHeaderFieldFirstChar(char decapitalizedFirstChar) {
			switch (decapitalizedFirstChar) {
				case 'c':
				case 't':
					return true;
				default:
					return false;
			}
		}

		protected virtual void ScanHeaderFieldValue(MessageBuffer messageBuffer, string decapitalizedFieldName, int startIndex) {
			// argument checks
			Debug.Assert(messageBuffer != null);

			string value;
			switch (decapitalizedFieldName) {
				case "content-length":
					value = messageBuffer.ReadHeaderFieldASCIIValue(decapitalize: false);
					this.ContentLength = MessageBuffer.ParseHeaderFieldValueAsLong(value);
					break;
				case "transfer-encoding":
					value = messageBuffer.ReadHeaderFieldASCIIValue(decapitalize: true);
					if (MessageBuffer.IsChunkedSpecified(value) == false) {
						throw MessageBuffer.CreateBadRequestException();
					}
					this.ContentLength = -1;	// -1 means 'chunked'
					break;
				default:
					// just skip
					messageBuffer.SkipHeaderField();
					break;
			}
		}

		protected virtual void ScanBody(MessageBuffer messageBuffer) {
			// argument checks
			Debug.Assert(messageBuffer != null);

			switch (this.ContentLength) {
				case -1:
					// chunked body
					messageBuffer.SkipChunkedBody();
					break;
				case 0:
					// no body
					break;
				default:
					// simple body
					messageBuffer.SkipBody(this.ContentLength);
					break;
			}

			return;
		}

		protected virtual void WriteHeader(MessageBuffer messageBuffer, IEnumerable<MessageBuffer.Modification> modifications) {
			// argument checks
			Debug.Assert(messageBuffer != null);
			// modifications can be null

			// write message header
			messageBuffer.WriteHeader(modifications);
		}

		protected virtual void WriteBody(MessageBuffer messageBuffer) {
			// argument checks
			Debug.Assert(messageBuffer != null);

			// write message body
			messageBuffer.WriteBody();
		}

		#endregion


		#region privates

		private void ResetThisClassLevelMessageProperties() {
			// reset message properties of this class level
			this.Version = null;
			this.ContentLength = 0;
			this.EndOfHeaderFields = MessageBuffer.Span.ZeroToZero;

			return;
		}

		#endregion
	}
}
