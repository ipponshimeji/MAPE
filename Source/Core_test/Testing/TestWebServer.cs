using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using MAPE.Test.TestWeb;
using Xunit;


namespace MAPE.Testing {
	public class TestWebServer {
		#region data - synchronized by classLocker

		private static readonly object classLocker = new object();

		private static int useCount = 0;

		private static TestWebServer instance = null;

		#endregion


		#region data

		public IPEndPoint HttpEndPoint { get; private set; }

		public IPEndPoint HttpsEndPoint { get; private set; }

		public IPEndPoint ProxyEndPoint { get; private set; }

		private Process process = null;

		#endregion


		#region creation and disposal

		private TestWebServer(IPEndPoint httpEndPoint, IPEndPoint httpsEndPoint, IPEndPoint proxyEndPoint) {
			// argument checks
			if (httpEndPoint == null && httpsEndPoint != null) {
				throw new ArgumentException("It must be null when 'httpEndPoint' is null.", nameof(httpsEndPoint));
			}
			if (httpEndPoint == null && proxyEndPoint != null) {
				throw new ArgumentException("It must be null when 'httpEndPoint' is null.", nameof(proxyEndPoint));
			}

			// initialize members
			this.HttpEndPoint = httpEndPoint;
			this.HttpsEndPoint = httpsEndPoint;
			this.ProxyEndPoint = proxyEndPoint;

			return;
		}

		private TestWebServer(): this(null, null, null) {
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
					// Note that the following code may throw an exception on error.
					Debug.Assert(instance == null);
					server = new TestWebServer();
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
			// this method must be called within the scope locked by classLocker
			Debug.Assert(Monitor.IsEntered(classLocker));

			// start process
			string serverFilePath = GetServerFilePath();

			ProcessStartInfo info = new ProcessStartInfo();
			info.FileName = "dotnet";
			info.CreateNoWindow = true;
			info.RedirectStandardInput = true;
			info.RedirectStandardError = true;
			info.RedirectStandardOutput = true;
			info.StandardOutputEncoding = Encoding.Default;
			info.StandardErrorEncoding = Encoding.Default;
			info.UseShellExecute = false;

			string createArguments(IPEndPoint httpEP, IPEndPoint httpsEP, IPEndPoint proxyEP) {
				// argument checks
				Debug.Assert(httpEP != null);

				string arguments = $"\"{serverFilePath}\" \"http://{httpEP}/\"";
				if (httpsEP != null) {
					arguments = string.Concat(arguments, $" \"https://{httpsEP}/\"");
				} else if (proxyEP != null) {
					Debug.Assert(httpsEP == null);
					arguments = string.Concat(arguments, " \"\"");
				}
				if (proxyEP != null) {
					arguments = string.Concat(arguments, $" \"http://{proxyEP}/\"");
				}

				return arguments;
			}

			Process startServer(ProcessStartInfo psi, IPEndPoint httpEP, IPEndPoint httpsEP, IPEndPoint proxyEP) {
				// argument checks
				Debug.Assert(psi != null);

				// start server
				psi.Arguments = createArguments(httpEP, httpsEP, proxyEP);
				Process p = Process.Start(psi);
				bool isStarted = TestWebSettings.ReadWhetherServerIsStarted(p);
				if (isStarted == false) {
					int timeoutMilliseconds = 3000;
					if (p.WaitForExit(timeoutMilliseconds) == false) {
						p.Kill();
						p.WaitForExit();
					}
				}

				return p;
			}

			IPEndPoint httpEndPoint = this.HttpEndPoint;
			IPEndPoint httpsEndPoint = this.HttpsEndPoint;
			IPEndPoint proxyEndPoint = this.ProxyEndPoint;
			if (httpEndPoint != null) {
				// start server with specified end points
				process = startServer(info, httpEndPoint, httpsEndPoint, proxyEndPoint);
			} else {
				// start server with automatically allocated ports
				IPAddress address = IPAddress.Loopback;
				int retryCount = 2;
				int count = 0;
				do {
					// find free ports
					int[] ports = TestUtil.GetFreePortToListen(address, 3);
					httpEndPoint = new IPEndPoint(address, ports[0]);
					httpsEndPoint = new IPEndPoint(address, ports[1]);
					proxyEndPoint = new IPEndPoint(address, ports[2]);

					// try to start
					process = startServer(info, httpEndPoint, httpsEndPoint, proxyEndPoint);
					if (process.HasExited == false) {
						// succeeded
						this.HttpEndPoint = httpEndPoint;
						this.HttpsEndPoint = httpsEndPoint;
						this.ProxyEndPoint = proxyEndPoint;
						break;
					} else if (process.ExitCode != TestWebSettings.EndPointInUseExitCode || retryCount <= count) {
						// failed
						break;
					}

					// maybe the found port has been used at the moment
					// retry with new ports
					process.Dispose();
					process = null;
					++count;
				} while (true);
			}
			if (process.HasExited) {
				string message = process.StandardError.ReadToEnd();
				if (string.IsNullOrEmpty(message)) {
					message = "Failed to start Test Web Server.";
				}
				process.Dispose();
				throw new InvalidOperationException(message);
			}

			// update state
			this.process = process;
		}

		private void Stop() {
			// state checks
			Process process = this.process;
			this.process = null;
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
			process.Dispose();
		}

		#endregion
	}
}
