using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Xunit;


namespace MAPE.Testing {
	public class TestWebServer {
		#region data

		private static readonly object classLocker = new object();

		private static int useCount = 0;

		private static TestWebServer instance = null;

		#endregion


		#region data

		public IPEndPoint ProxyEndPoint { get; }

		public IPEndPoint DirectEndPoint { get; }

		private Process process = null;

		#endregion


		#region creation and disposal

		private TestWebServer(IPEndPoint proxyEndPoint, IPEndPoint directEndPoint) {
			// argument checks
			if (proxyEndPoint == null) {
				throw new ArgumentNullException(nameof(proxyEndPoint));
			}
			// directEndPoint can be null

			// initialize members
			this.ProxyEndPoint = proxyEndPoint;
			this.DirectEndPoint = directEndPoint;

			return;
		}

		#endregion


		#region methods

		public static TestWebServer Use() {
			TestWebServer server;
			lock (classLocker) {
				// state checks
				if (useCount < 0) {
					throw new InvalidOperationException("invalid state");
				}
				if (useCount == Int32.MaxValue) {
					throw new InvalidOperationException("use count overflow");
				}

				// create an instance if necessary
				if (useCount == 0) {
					// Note that the following block may throw an exception on error.
					Debug.Assert(instance == null);
					server = CreateTestWebServer();
					server.Start();
					instance = server;
				} else {
					Debug.Assert(instance != null);
					server = instance;
				}

				// increment the use count
				++useCount;
			}

			return server;
		}

		public static void Unuse() {
			lock (classLocker) {
				// state checks
				if (useCount <= 0) {
					throw new InvalidOperationException("invalid state");
				}

				// destroy the instance if necessary
				if (--useCount == 0) {
					TestWebServer server = instance;
					instance = null;
					Debug.Assert(server != null);
					server.Stop();
				}
			}
		}

		#endregion


		#region privates

		private static string GetServerFilePath() {
			string dirPath = Path.GetDirectoryName(typeof(TestWebServer).Assembly.ManifestModule.FullyQualifiedName);
			return Path.Combine(dirPath, "TestWebServer.dll");			
		}

		private static TestWebServer CreateTestWebServer() {
			IPAddress address = IPAddress.Loopback;
			int[] ports = TestUtil.GetFreePortToListen(address, 2);
			IPEndPoint proxyEndPoint = new IPEndPoint(address, ports[0]);
			IPEndPoint directEndPoint = new IPEndPoint(address, ports[1]);

			return new TestWebServer(proxyEndPoint, directEndPoint);
		}

		private void Start() {
			// state checks
			Process process = this.process;
			if (process != null) {
				return;
			}
			// this method must be called within the scope locked by classLocker
			Debug.Assert(Monitor.IsEntered(classLocker));

			// start process
			string serverFilePath = GetServerFilePath();
			string proxyPrefix = $"http://{this.ProxyEndPoint}/";
			string directPrefix = $"http://{this.DirectEndPoint}/";

			ProcessStartInfo info = new ProcessStartInfo();
			info.FileName = "dotnet";
			info.Arguments = $"\"{serverFilePath}\" \"{proxyPrefix}\" \"{directPrefix}\"";
			info.CreateNoWindow = true;
			info.RedirectStandardInput = true;
			info.RedirectStandardError = true;
			info.RedirectStandardOutput = true;
			info.StandardOutputEncoding = Encoding.UTF8;
			info.UseShellExecute = false;

			process = Process.Start(info);
			if (process.HasExited) {
				string error = process.StandardError.ReadToEnd();
				if (string.IsNullOrEmpty(error)) {
					error = "Failed to start Test Web Server.";
				}
				throw new InvalidOperationException(error);
			}
			this.process = process;
		}

		private void Stop() {
			// state checks
			Process process = this.process;
			if (process == null) {
				return;
			}
			// this method must be called within the scope locked by classLocker
			Debug.Assert(Monitor.IsEntered(classLocker));

			try {
				// input for "Hit Enter key to quit."
				process.StandardInput.WriteLine();
			} catch (Exception e) {
				// continue
				Console.WriteLine(e);
			}
			process.WaitForExit();
		}

		#endregion
	}
}
