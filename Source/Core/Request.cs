using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;


namespace MAPE.Core {
	public class Request: Message {
		#region data

		public string Method {
			get;
			protected set;
		}

		#endregion


		#region creation and disposal

		public Request(): base() {
			// initialize members
			this.Method = null;

			return;
		}

		#endregion


		#region overrides/overridables

		protected override void ResetMessageProperties() {
			// reset this class level
			this.Method = null;

			// reset the base class level
			base.ResetMessageProperties();
		}

		protected override void ScanStartLine(MessageBuffer messageBuffer) {
			// argument checks
			Debug.Assert(messageBuffer != null);

			// read items
			string method = messageBuffer.ReadStartLineItem(skipItem: false, decapitalize: false, lastItem: false);
			messageBuffer.ReadStartLineItem(skipItem: true, decapitalize: false, lastItem: false);
			string version = messageBuffer.ReadStartLineItem(skipItem: false, decapitalize: false, lastItem: true);

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
				default:
					base.ScanHeaderFieldValue(messageBuffer, decapitalizedFieldName, startIndex);
					break;
			}
		}

		#endregion
	}
}
