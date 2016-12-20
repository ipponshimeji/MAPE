using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;


namespace MAPE.Http {
	public class Request: Message {
		#region data

		public string Method {
			get;
			protected set;
		}

		public MessageBuffer.Span ProxyAuthorizationSpan {
			get;
			protected set;
		}

		#endregion


		#region creation and disposal

		public Request(): base() {
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

		protected override void ScanStartLine(MessageBuffer messageBuffer) {
			// argument checks
			Debug.Assert(messageBuffer != null);

			// read items
			string method = messageBuffer.ReadSpaceSeparatedItem(skipItem: false, decapitalize: false, lastItem: false);
			messageBuffer.ReadSpaceSeparatedItem(skipItem: true, decapitalize: false, lastItem: false);
			string version = messageBuffer.ReadSpaceSeparatedItem(skipItem: false, decapitalize: false, lastItem: true);

			// set message properties
			this.Method = method;
			this.Version = MessageBuffer.ParseVersion(version);

			return;
		}

		protected override bool IsInterestingHeaderFieldFirstChar(char decapitalizedFirstChar) {
			switch (decapitalizedFirstChar) {
				case 'p':
					return true;
				default:
					return base.IsInterestingHeaderFieldFirstChar(decapitalizedFirstChar);
			}
		}

		protected override void ScanHeaderFieldValue(MessageBuffer messageBuffer, string decapitalizedFieldName, int startIndex) {
			switch (decapitalizedFieldName) {
				case "proxy-authorization":
					// save its span, but value is unnecessary
					messageBuffer.SkipHeaderField();
					this.ProxyAuthorizationSpan = new MessageBuffer.Span(startIndex, messageBuffer.CurrentHeaderIndex);
					break;
				default:
					base.ScanHeaderFieldValue(messageBuffer, decapitalizedFieldName, startIndex);
					break;
			}
		}

		#endregion


		#region privates

		private void ResetThisClassLevelMessageProperties() {
			// reset message properties of this class level
			this.Method = null;
			this.ProxyAuthorizationSpan = MessageBuffer.Span.ZeroToZero;

			return;
		}

		#endregion
	}
}
