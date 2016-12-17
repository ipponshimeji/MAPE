using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;


namespace MAPE.Core {
	public class Response: Message {
		#region data

		public int StatusCode {
			get;
			protected set;
		}

		#endregion


		#region creation and disposal

		public Response(): base() {
			// initialize members
			this.StatusCode = 0;

			return;
		}

		#endregion


		#region overrides/overridables

		protected override void ResetMessageProperties() {
			// reset this class level

			// reset the base class level
			base.ResetMessageProperties();
		}

		protected override void ScanStartLine(MessageBuffer messageBuffer) {
			// argument checks
			Debug.Assert(messageBuffer != null);

			// read items
			string version = messageBuffer.ReadStartLineItem(skipItem: false, decapitalize: false, lastItem: false);
			string statusCode = messageBuffer.ReadStartLineItem(skipItem: false, decapitalize: false, lastItem: false);
			messageBuffer.ReadStartLineItem(skipItem: true, decapitalize: false, lastItem: true);

			// set message properties
			this.Version = MessageBuffer.ParseVersion(version);
			this.StatusCode = MessageBuffer.ParseStatusCode(statusCode);

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
				case "proxy-authenticate":
				default:
					base.ScanHeaderFieldValue(messageBuffer, decapitalizedFieldName, startIndex);
					break;
			}
		}

		#endregion
	}
}
