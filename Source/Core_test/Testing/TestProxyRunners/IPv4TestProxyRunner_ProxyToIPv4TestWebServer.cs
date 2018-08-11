using System;
using System.Diagnostics;
using System.Net;
using MAPE.Testing.TestWebServerRunners;


namespace MAPE.Testing.TestProxyRunners {
	public class IPv4TestProxyRunner_ProxyToIPv4TestWebServer: TestProxyRunner {
		#region data

		public static readonly IPv4TestWebServerRunner IPv4TestWebServerRunner = SharedInstanceProvider<IPv4TestWebServerRunner>.SharedInstance;

		#endregion


		#region creation

		public IPv4TestProxyRunner_ProxyToIPv4TestWebServer(): base(testWebServerRunner: IPv4TestWebServerRunner, proxySettings: null, directMode: false) {
		}

		#endregion
	}
}
