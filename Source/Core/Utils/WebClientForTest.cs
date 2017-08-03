using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Cache;


namespace MAPE.Utils {
	public class WebClientForTest: WebClient {
		#region constants

		public int Timeout { get; set; } = 100 * 1000;

		#endregion


		#region creation and disposal

		public WebClientForTest(): base() {
			// customize members
			this.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);

			return;
		}

		#endregion


		#region overrides

		protected override WebRequest GetWebRequest(Uri address) {
			WebRequest webRequest = base.GetWebRequest(address);
			webRequest.Timeout = this.Timeout;
			return webRequest;
		}

		#endregion
	}
}
