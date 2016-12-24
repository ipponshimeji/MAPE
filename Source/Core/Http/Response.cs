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

		public int StatusCode {
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
			this.Version = HeaderBuffer.ParseVersion(version);
			this.StatusCode = HeaderBuffer.ParseStatusCode(statusCode);

			return;
		}

		protected override bool IsInterestingHeaderFieldFirstChar(char decapitalizedFirstChar) {
			switch (decapitalizedFirstChar) {
				case 'p':   // possibly "proxy-authenticate"
					return true;
				default:
					return base.IsInterestingHeaderFieldFirstChar(decapitalizedFirstChar);
			}
		}

		protected override void ScanHeaderFieldValue(HeaderBuffer headerBuffer, string decapitalizedFieldName, int startOffset) {
			switch (decapitalizedFieldName) {
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
			this.ProxyAuthenticateSpan = MessageBuffer.Span.ZeroToZero;
			this.ProxyAuthenticateValue = null;

			return;
		}

		#endregion
	}
}
