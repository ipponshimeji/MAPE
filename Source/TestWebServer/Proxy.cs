using System;
using System.Diagnostics;
using System.Net;

namespace MAPE.Test.TestWebServer {
    public class Proxy: ServerBase {
		#region data

		protected string ProxyPrefix { get; }

		#endregion


		#region creation and disposal

		public Proxy(string proxyPrefix): base() {
			// argument checks
			if (string.IsNullOrEmpty(proxyPrefix)) {
				throw new ArgumentNullException(nameof(proxyPrefix));
			}

			// initialize members
			this.ProxyPrefix = proxyPrefix;

			return;
		}

		#endregion


		#region overridables

		protected override void SetupListener(HttpListener listener) {
			// argument checks
			Debug.Assert(listener != null);

			base.SetupListener(listener);
			listener.Prefixes.Add(this.ProxyPrefix);
		}

		protected override AuthenticationSchemes SelectAuthenticationScheme(HttpListenerRequest request) {
			// argument checks
			Debug.Assert(request != null);

			// handle authentication manually because HttpListener does not support authentication as a proxy 
			return AuthenticationSchemes.Anonymous;
		}

		#endregion
	}
}
