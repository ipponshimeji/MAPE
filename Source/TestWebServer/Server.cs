using System;
using System.Diagnostics;
using System.Net;
using MAPE.Test.TestWeb;

namespace MAPE.Test.TestWebServer {
    public class Server: ServerBase {
		#region data

		protected string HttpPrefix { get; }

		protected string HttpsPrefix { get; }

		#endregion


		#region creation and disposal

		public Server(string httpPrefix, string httpsPrefix): base() {
			// argument checks
			if (string.IsNullOrEmpty(httpPrefix)) {
				throw new ArgumentNullException(nameof(httpPrefix));
			}
			if (string.IsNullOrEmpty(httpsPrefix)) {
				// httpsPrefix can be null or empty
				// normalize to null
				httpsPrefix = null;
			}

			// initialize members
			this.HttpPrefix = httpPrefix;
			this.HttpsPrefix = httpsPrefix;

			return;
		}

		#endregion


		#region overridables

		protected override void SetupListener(HttpListener listener) {
			// argument checks
			Debug.Assert(listener != null);

			base.SetupListener(listener);
			listener.Prefixes.Add(this.HttpPrefix);
			if (this.HttpsPrefix != null) {
				Debug.Assert(0 < this.HttpsPrefix.Length);
				listener.Prefixes.Add(this.HttpsPrefix);
			}
		}

		protected override AuthenticationSchemes SelectAuthenticationScheme(HttpListenerRequest request) {
			// argument checks
			Debug.Assert(request != null);

			switch (request.RawUrl) {
				case Responses.BasicAuthenticationPath:
					return AuthenticationSchemes.Basic;
				default:
					return base.SelectAuthenticationScheme(request);
			}
		}

		#endregion
	}
}
