using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Xunit;
using MAPE.Test.TestWeb;
using MAPE.Testing;
using MAPE.Testing.TestWebServerRunners;
using MAPE.Testing.TestProxyRunners;


namespace MAPE.Server.Test {
	public class ProxyingTest {
		#region class fixtures

		[CollectionDefinition("IPv4Proxy, ProxyToIPv4TestWebServer")]
		public class IPv4Proxy_ProxyToIPv4TestWebServer:
			ICollectionFixture<SharedInstanceProvider<IPv4TestWebServerRunner>.Fixture>,
			ICollectionFixture<SharedInstanceProvider<IPv4TestProxyRunner_ProxyToIPv4TestWebServer>.Fixture>
		{
		}

		[CollectionDefinition("IPv4Proxy, DirectToIPv4TestWebServer")]
		public class IPv4Proxy_DirectToIPv4TestWebServer:
			ICollectionFixture<SharedInstanceProvider<IPv4TestWebServerRunner>.Fixture>,
			ICollectionFixture<SharedInstanceProvider<IPv4TestProxyRunner_DirectToIPv4TestWebServer>.Fixture>
		{
		}

		#endregion


		#region data

		protected static readonly IPv4TestWebServerRunner IPv4TestWebServerRunner = SharedInstanceProvider<IPv4TestWebServerRunner>.SharedInstance;

		#endregion


		#region test base

		public abstract class TestBase {
			#region data

			private readonly TestWebServerRunner testWebServerRunner;
			private readonly TestProxyRunner testProxyRunner;

			#endregion


			#region properties

			protected TestProxyRunner TestProxyRunner {
				get {
					return this.testProxyRunner;
				}
			}

			#endregion


			#region creation

			public TestBase(TestWebServerRunner testWebServerRunner, TestProxyRunner testProxyRunner) {
				// argument checks
				if (testWebServerRunner == null) {
					throw new ArgumentNullException(nameof(testWebServerRunner));
				}
				if (testProxyRunner == null) {
					throw new ArgumentNullException(nameof(testProxyRunner));
				}
				if (testWebServerRunner != testProxyRunner.ServerRunner) {
					throw new ArgumentNullException($"Inconsistent with {nameof(testProxyRunner)}.ServerRunner", nameof(testWebServerRunner));
				}

				// initialize members
				this.testWebServerRunner = testWebServerRunner;
				this.testProxyRunner = testProxyRunner;
			}

			#endregion


			#region methods

			protected static void AssertEqualResponse(HttpResponseMessage expected, HttpWebResponse actual, IEnumerable<string> additionalHeaderNames) {
				TestUtil.AssertEqualResponse(expected, actual, additionalHeaderNames);
			}

			protected static void AssertEqualResponse(HttpResponseMessage expected, HttpWebResponse actual) {
				TestUtil.AssertEqualResponse(expected, actual);
			}

			protected HttpWebRequest CreateBaseRequest(string path, bool https = false) {
				return this.testProxyRunner.CreateBaseRequest(path, https);
			}

			#endregion


			#region tests

			[Fact(DisplayName = "simple")]
			public void Simple() {
				// ARRANGE
				string path = "/simple";

				// ACT
				HttpWebRequest request = CreateBaseRequest(path);
				HttpWebResponse actual = (HttpWebResponse)request.GetResponse();

				// ASSERT
				AssertEqualResponse(Responses.GetResponse(path), actual);
			}

			#endregion
		}

		#endregion


		#region tests

		/// <summary>
		/// <list type="bullet">
		///   <item><term>Server</term><description>IPv4 server</description>
		///   <item><term>Proxying</term><description>proxy to server</description>
		///   <item><term>Proxy</term><description>IPv4 listener</description>
		/// </list>
		/// </summary>
		[Collection("IPv4Proxy, ProxyToIPv4TestWebServer")]
		public class IPv4_ProxyToIPv4Server: TestBase {
			#region creation

			public IPv4_ProxyToIPv4Server(
				SharedInstanceProvider<IPv4TestWebServerRunner>.Fixture testWebServerRunnerFixture,
				SharedInstanceProvider<IPv4TestProxyRunner_ProxyToIPv4TestWebServer>.Fixture testProxyRunnerFixture
			) : base(testWebServerRunnerFixture.SharedInstance, testProxyRunnerFixture.SharedInstance) {
			}

			#endregion
		}

		/// <summary>
		/// <list type="bullet">
		///   <item><term>Server</term><description>IPv4 server</description>
		///   <item><term>Proxying</term><description>direct to server</description>
		///   <item><term>Proxy</term><description>IPv4 listener</description>
		/// </list>
		/// </summary>
		[Collection("IPv4Proxy, DirectToIPv4TestWebServer")]
		public class IPv4_DirectToIPv4Server: TestBase {
			#region creation

			public IPv4_DirectToIPv4Server(
				SharedInstanceProvider<IPv4TestWebServerRunner>.Fixture testWebServerRunnerFixture,
				SharedInstanceProvider<IPv4TestProxyRunner_DirectToIPv4TestWebServer>.Fixture testProxyRunnerFixture
			): base(testWebServerRunnerFixture.SharedInstance, testProxyRunnerFixture.SharedInstance) {
			}

			#endregion
		}

		#endregion
	}
}
