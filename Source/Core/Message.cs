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
			this.Version = null;
			this.ContentLength = 0;

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

		public void Read() {
			// state checks
			MessageBuffer messageBuffer = this.messageBuffer;
			Debug.Assert(messageBuffer.CanRead);

			// read a message
			ScanStartLine(messageBuffer);
			bool emptyLine;
			do {
				emptyLine = ScanHeaderField(messageBuffer);
			} while (emptyLine == false);
			ScanBody(messageBuffer);

			return;
		}

		public void Write() {
			// state checks
			MessageBuffer messageBuffer = this.messageBuffer;
			Debug.Assert(messageBuffer.CanRead);

			// write a message
			WriteHeader(messageBuffer);
			WriteBody(messageBuffer);

			return;
		}

		#endregion


		#region overridables

		protected virtual void ResetMessageProperties() {
			// reset message properties
			this.ContentLength = 0;
			this.Version = null;

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
				case "content-encoding":
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

		protected virtual void WriteHeader(MessageBuffer messageBuffer) {
			// argument checks
			Debug.Assert(messageBuffer != null);

			// write message header
			messageBuffer.WriteHeader();
		}

		protected virtual void WriteBody(MessageBuffer messageBuffer) {
			// argument checks
			Debug.Assert(messageBuffer != null);

			// write message body
			messageBuffer.WriteBody();
		}

		#endregion


		#region privates

#if false
		private void ScanContentLength(StringBuilder workingBuf) {
			// argument checks
			Debug.Assert(workingBuf != null);

			workingBuf.Clear();
			ReadASCIIBefore(Separators.CRLF, workingBuf);

			this.ContentLength = long.Parse(workingBuf.ToString(), System.Globalization.NumberStyles.Integer);
		}

		private void ScanTransferEncoding(StringBuilder workingBuf) {
			// argument checks
			Debug.Assert(workingBuf != null);

			workingBuf.Clear();
			ReadASCIIBefore(Separators.CRLF, workingBuf);
			string value = workingBuf.ToString().Trim();    // ToDo: remove tab
			if (value.EndsWith("chunked", StringComparison.OrdinalIgnoreCase)) {
				// ToDo: not exact
				this.ContentLength = -1;
			}

			return;
		}
#endif

		#endregion
	}
}
