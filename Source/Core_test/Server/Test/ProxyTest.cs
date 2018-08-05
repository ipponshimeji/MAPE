using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Xunit;
using MAPE.Testing;


namespace MAPE.Server.Test {
	public class ProxyTest {
		#region temp

		[Fact(DisplayName = "Temp")]
		public void Temp() {
			TestWebServer server = TestWebServer.Use();
			try {
				;
			} finally {
				TestWebServer.Unuse();
			}
		}

		#endregion
	}
}
