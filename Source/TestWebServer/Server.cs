using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace MAPE.Test.TestWebServer {
    class Server {
		#region properties

		protected string ProxyPrefix { get; }

		protected string DirectPrefix { get; }

		protected HttpListener Listener { get; private set; }

		#endregion


		#region creation and disposal

		public Server(string proxyPrefix, string directPrefix) {
			// argument checks
			if (string.IsNullOrEmpty(proxyPrefix)) {
				throw new ArgumentNullException(nameof(proxyPrefix));
			}
			// directPrefix can be null

			// initialize members
			this.ProxyPrefix = proxyPrefix;
			this.DirectPrefix = directPrefix;

			return;
		}

		#endregion


		#region methods

		public void Start() {
			HttpListener listener = new HttpListener();
			try {
				// setup HttpListener
				listener.Prefixes.Add(this.ProxyPrefix);
				if (this.DirectPrefix != null) {
					Listener.Prefixes.Add(this.DirectPrefix);
				}

				listener.Start();
				this.Listener = listener;
			} catch {
			}
		}

		public void Stop() {
			// state checks
			Debug.Assert(this.Listener != null);

			this.Listener.Stop();
		}

		public void Handle(HttpListenerContext context) {
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;


		}

		#endregion
	}
}
