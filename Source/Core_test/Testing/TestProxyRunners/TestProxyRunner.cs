using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using MAPE.Server;
using MAPE.Server.Settings;
using MAPE.Command.Settings;
using MAPE.Testing;
using MAPE.Testing.TestWebServerRunners;

namespace MAPE.Testing.TestProxyRunners {
	public class TestProxyRunner: ObjectWithUseCount, IDisposable, IActualProxy, IProxyRunner, IWebProxy {
		#region data

		private readonly TestWebServerRunner testWebServerRunner;

		private readonly ProxySettings proxySettings;

		private readonly IServerComponentFactory componentFactory;

		private readonly bool directMode;

		#endregion


		#region data - synchronized by base.useCountLocker

		protected Proxy Proxy { get; private set; } = null;

		protected Uri ProxyUri { get; private set; } = null;

		#endregion


		#region properties

		public TestWebServerRunner ServerRunner {
			get {
				return this.testWebServerRunner;
			}
		}

		public bool DirectMode {
			get {
				return this.directMode;
			}
		}

		public IPEndPoint ServerHttpEndPoint {
			get {
				return this.testWebServerRunner.HttpEndPoint;
			}
		}

		public IPEndPoint ServerHttpsEndPoint {
			get {
				return this.testWebServerRunner.HttpsEndPoint;
			}
		}

		public IPEndPoint ProxyMainListenerEndPoint {
			get {
				IPEndPoint value = null;
				Proxy proxy = this.Proxy;
				if (proxy != null) {
					value = proxy.MainListenerEndPoint;
				}

				return value;
			}
		}

		#endregion


		#region creation and disposal

		public TestProxyRunner(TestWebServerRunner testWebServerRunner, ProxySettings proxySettings = null, bool directMode = false, IServerComponentFactory componentFactory = null) {
			// argument checks
			if (testWebServerRunner == null) {
				throw new ArgumentNullException(nameof(testWebServerRunner));
			}
			if (proxySettings == null) {
				proxySettings = CreateDefaultProxySettings(IPAddress.Loopback);
			}
			if (componentFactory == null) {
				componentFactory = CreateDefaultServerComponentFactory();
			}

			// initialize members
			this.testWebServerRunner = testWebServerRunner;
			this.proxySettings = proxySettings;
			this.componentFactory = componentFactory;
			this.directMode = directMode;
		}

		public virtual void Dispose() {
			Debug.Assert(this.UseCount == 0);
		}

		#endregion


		#region methods

		public ProxySettings CreateDefaultProxySettings(IPAddress address) {
			// argument checks
			if (address == null) {
				throw new ArgumentNullException(nameof(address));
			}

			// create default ProxySettings object
			ListenerSettings listenerSettings = new ListenerSettings();
			listenerSettings.Address = address;
			listenerSettings.Port = 0;

			ProxySettings settings = new ProxySettings();
			settings.MainListener = listenerSettings;
			return settings;
		}

		public IServerComponentFactory CreateDefaultServerComponentFactory() {
			return new MAPE.ComponentFactory();
		}

		public IPEndPoint GetProxyListenerEndPoint(int index) {
			throw new NotImplementedException();
		}

		public string GetUri(string path, bool https = false) {
			// argument checks
			if (string.IsNullOrEmpty(path)) {
				throw new ArgumentNullException(nameof(path));
			}
			if (path.StartsWith("/") == false) {
				throw new ArgumentException("It must start with '/'", nameof(path));
			}

			string uri;
			if (https) {
				uri = $"https://{this.ServerHttpsEndPoint}{path}";
			} else {
				uri = $"http://{this.ServerHttpEndPoint}{path}";
			}

			return uri;
		}

		public HttpWebRequest CreateBaseRequest(string path, bool https = false) {
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(GetUri(path, https));
			request.Proxy = this;

			return request;
		}

		#endregion


		#region IActualProxy

		public virtual string Description {
			get {
				return "test web server";
			}
		}

		public virtual IReadOnlyCollection<DnsEndPoint> GetProxyEndPoints(DnsEndPoint targetEndPoint) {
			return GetProxyEndPoints();
		}

		public virtual IReadOnlyCollection<DnsEndPoint> GetProxyEndPoints(Uri targetUri) {
			return GetProxyEndPoints();
		}

		#endregion


		#region IProxyRunner

		public virtual CredentialSettings GetCredential(string endPoint, string realm, bool needUpdate) {
			throw new NotImplementedException();
		}

		#endregion


		#region IWebProxy

		public virtual ICredentials Credentials {
			get {
				return null;
			}
			set {
			}
		}

		public virtual Uri GetProxy(Uri destination) {
			return this.ProxyUri;
		}

		public virtual bool IsBypassed(Uri host) {
			return false;
		}

		#endregion


		#region overrides

		protected override void OnUsed() {
			// state checks
			Proxy proxy = this.Proxy;
			if (proxy != null) {
				return;
			}

			// preparation
			TestWebServerRunner server = this.testWebServerRunner;
			Debug.Assert(server != null);
			IServerComponentFactory componentFactory = this.componentFactory;
			Debug.Assert(componentFactory != null);

			// ensuer that the test web server is started
			server.Use();
			try {
				// start the proxy
				proxy = componentFactory.CreateProxy(proxySettings);
				try {
					proxy.ActualProxy = this;
					proxy.Start(this);
				} catch {
					proxy.Dispose();
					throw;
				}
			} catch {
				server.Unuse();
				throw;
			}

			// update state
			this.Proxy = proxy;
			this.ProxyUri = new Uri($"http://{proxy.MainListenerEndPoint}");
		}

		protected override void OnUnused() {
			// state checks
			Proxy proxy = this.Proxy;
			this.Proxy = null;
			this.ProxyUri = null;
			if (proxy == null) {
				return;
			}

			// stop the proxy
			try {
				proxy.Stop();
				proxy.Dispose();
			} finally {
				this.testWebServerRunner.Unuse();
			}
		}

		#endregion


		#region overridables

		public IReadOnlyCollection<DnsEndPoint> GetProxyEndPoints() {
			DnsEndPoint[] result = null;
			if (this.directMode == false) {
				// Note that proxyEndPoint will be null if the server is not running.
				IPEndPoint proxyEndPoint = this.testWebServerRunner.ProxyEndPoint;
				if (proxyEndPoint != null) {
					result = new DnsEndPoint[] {
						new DnsEndPoint(proxyEndPoint.Address.ToString(), proxyEndPoint.Port)
					};
				}
			}

			return result;
		}

		#endregion
	}
}
