using System;
using System.Collections.Generic;
using System.Diagnostics;
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
