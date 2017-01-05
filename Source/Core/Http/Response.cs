using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;


namespace MAPE.Http {
	public class Response: Message {
		#region data

		// Note that Request is not message property
		protected Request Request { get; private set; } = null;

		#endregion


		#region data - message properties

		public int StatusCode {
			get;
			protected set;
		}

		public bool KeepAliveEnabled {
			get;
			protected set;
		}

		public MessageBuffer.Span ProxyAuthenticateSpan {
			get;
			protected set;
		}

		public string ProxyAuthenticateValue {
			get;
			protected set;
		}

		#endregion


		#region creation and disposal

		public Response(): base() {
			// initialize members
			ResetThisClassLevelMessageProperties();

			return;
		}

		#endregion


		#region methods

		public static void RespondSimpleError(Stream output, int statusCode, string reasonPhrase) {
			// argument checks
			if (output == null) {
				throw new ArgumentNullException(nameof(output));
			}
			if (output.CanWrite == false) {
				throw new ArgumentException("It is not writable", nameof(output));
			}
			if (statusCode < 0 || 1000 <= statusCode) {
				throw new ArgumentOutOfRangeException(nameof(statusCode));
			}
			if (reasonPhrase == null) {
				reasonPhrase = string.Empty;
			}

			// build message bytes
			string message = $"HTTP/1.0 {statusCode.ToString()} {reasonPhrase}\r\n\r\n";
			byte[] messageBytes = Encoding.ASCII.GetBytes(message);

			// output the message
			output.Write(messageBytes, 0, messageBytes.Length);
			output.Flush();

			return;
		}

		public void RespondSimpleError(int statusCode, string reasonPhrase) {
			RespondSimpleError(this.Output, statusCode, reasonPhrase);
		}

		public bool Read(Request request) {
			// argument
			// request can be null

			// state checks
			if (this.Request != null) {
				throw new InvalidOperationException("This object is reading now.");
			}

			// set this.Request during reading message.
			this.Request = request;
			try {
				return base.Read();
			} finally {
				this.Request = null;
			}
		}

		#endregion


		#region overrides/overridables

		protected override void ResetMessageProperties() {
			// reset this class level
			ResetThisClassLevelMessageProperties();

			// reset the base class level
			base.ResetMessageProperties();
		}

		protected override void ScanStartLine(HeaderBuffer headerBuffer) {
			// argument checks
			Debug.Assert(headerBuffer != null);

			// read items
			string version = headerBuffer.ReadSpaceSeparatedItem(skipItem: false, decapitalize: false, lastItem: false);
			string statusCode = headerBuffer.ReadSpaceSeparatedItem(skipItem: false, decapitalize: false, lastItem: false);
			headerBuffer.ReadSpaceSeparatedItem(skipItem: true, decapitalize: false, lastItem: true);

			// set message properties
			Version httpVersion = HeaderBuffer.ParseVersion(version);
			this.Version = httpVersion;
			this.StatusCode = HeaderBuffer.ParseStatusCode(statusCode);
			if (httpVersion.Major == 1 && httpVersion.Minor == 0) {
				// in HTTP/1.0, keep-alive is disabled by default 
				this.KeepAliveEnabled = false;
			}

			return;
		}

		protected override bool IsInterestingHeaderFieldFirstChar(char decapitalizedFirstChar) {
			switch (decapitalizedFirstChar) {
				case 'c':   // possibly "connection"
					return true;
				case 'p':   // possibly "proxy-authenticate"
					return true;
				default:
					return base.IsInterestingHeaderFieldFirstChar(decapitalizedFirstChar);
			}
		}

		protected override void ScanHeaderFieldValue(HeaderBuffer headerBuffer, string decapitalizedFieldName, int startOffset) {
			switch (decapitalizedFieldName) {
				case "content-length":
				case "transfer-encoding":
					// Do not parse these header fields in response of HEAD method
					// otherwise it will be blocked to try to read body stream after this.
					if (this.Request?.Method != "HEAD") {
						base.ScanHeaderFieldValue(headerBuffer, decapitalizedFieldName, startOffset);
					}
					break;
				case "connection":
					// ToDo: exact parsing
					string value = headerBuffer.ReadFieldASCIIValue(false);
					if (value.Contains("close")) {
						this.KeepAliveEnabled = false;
					} else if (value.Contains("keep-alive")) {
						this.KeepAliveEnabled = true;
					}
					break;
				case "proxy-authenticate":
					// save its span and value
					this.ProxyAuthenticateValue = headerBuffer.ReadFieldASCIIValue(false);
					this.ProxyAuthenticateSpan = new MessageBuffer.Span(startOffset, headerBuffer.CurrentOffset);
					break;
				default:
					base.ScanHeaderFieldValue(headerBuffer, decapitalizedFieldName, startOffset);
					break;
			}
		}

		#endregion


		#region privates

		private void ResetThisClassLevelMessageProperties() {
			// reset message properties of this class level
			this.StatusCode = 0;
			this.KeepAliveEnabled = true;
			this.ProxyAuthenticateSpan = MessageBuffer.Span.ZeroToZero;
			this.ProxyAuthenticateValue = null;

			// Note that this.Request is not message property.

			return;
		}

		#endregion
	}
}
