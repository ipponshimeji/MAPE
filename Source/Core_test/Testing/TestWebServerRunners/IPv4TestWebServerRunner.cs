using System;
using System.Diagnostics;
using System.Net;


namespace MAPE.Testing.TestWebServerRunners {
	public class IPv4TestWebServerRunner: TestWebServerRunner {
		#region creation and disposal

		public IPv4TestWebServerRunner(): base(IPAddress.Loopback) {
		}

		#endregion
	}
}
