using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Xunit;


namespace MAPE.Testing {
	public class TestWebServer {
		#region data

		private static readonly object classLocker = new object();

		private static int useCount = 0;

		private static TestWebServer instance = null;

		#endregion


		#region data

		public string ProxyPrefix { get; private set; }

		public string DirectPrefix { get; private set; }

		private Process process = null;

		#endregion


		#region creation and disposal

		private TestWebServer(string proxyPrefix, string directPrefix = null) {
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
					server = new TestWebServer("http://127.0.0.1:8080/");
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

		private void Start() {
			// state checks
			Process process = this.process;
			if (process != null) {
				return;
			}

			// start process
			string serverFilePath = GetServerFilePath();
			IPAddress address = IPAddress.Loopback;
			int[] ports = TestUtil.GetFreePortToListen(address, 2);
			this.ProxyPrefix = $"http://{address.ToString()}:{ports[0]}/";
			this.DirectPrefix = $"http://{address.ToString()}:{ports[1]}/";

			ProcessStartInfo info = new ProcessStartInfo();
			info.FileName = "dotnet";
			info.Arguments = $"\"{serverFilePath}\" \"{this.ProxyPrefix}\" \"{this.DirectPrefix}\"";
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

			this.DirectPrefix = null;
			this.ProxyPrefix = null;
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
