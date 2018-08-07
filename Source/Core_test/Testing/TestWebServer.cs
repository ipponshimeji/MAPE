using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Xunit;


namespace MAPE.Testing {
	public class TestWebServer {
		#region data - synchronized by classLocker

		private static readonly object classLocker = new object();

		private static int useCount = 0;

		private static TestWebServer instance = null;

		#endregion


		#region data

		public IPEndPoint ProxyEndPoint { get; private set; }

		public IPEndPoint DirectEndPoint { get; private set; }

		private Process process = null;

		#endregion


		#region creation and disposal

		private TestWebServer(IPEndPoint proxyEndPoint, IPEndPoint directEndPoint) {
			// argument checks
			// proxyEndPoint and directEndPoint can be null

			// initialize members
			this.ProxyEndPoint = proxyEndPoint;
			this.DirectEndPoint = directEndPoint;

			return;
		}

		private TestWebServer(): this(null, null) {
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

			string createArguments(IPEndPoint proxyEP, IPEndPoint directEP) {
				// argument checks
				Debug.Assert(proxyEP != null);
				// directEP can be null

				string arguments = $"\"{serverFilePath}\" \"http://{proxyEP}/\"";
				if (directEP != null) {
					arguments = string.Concat(arguments, $" \"http://{directEP}/\"");
				}

				return arguments;
			}

			Process startServer(ProcessStartInfo psi, IPEndPoint proxyEP, IPEndPoint directEP) {
				// argument checks
				Debug.Assert(psi != null);

				// start server
				psi.Arguments = createArguments(proxyEP, directEP);
				Process p = Process.Start(psi);
				string state = p.StandardOutput.ReadLine();
				if (state != "Started.") {
					int timeoutMilliseconds = 3000;
					if (p.WaitForExit(timeoutMilliseconds) == false) {
						p.Kill();
						p.WaitForExit();
					}
				}

				return p;
			}

			IPEndPoint proxyEndPoint = this.ProxyEndPoint;
			IPEndPoint directEndPoint = this.DirectEndPoint;
			if (proxyEndPoint != null) {
				// start server with specified end points
				process = startServer(info, proxyEndPoint, directEndPoint);
			} else {
				// start server with automatically allocated ports
				IPAddress address = IPAddress.Loopback;
				int retryCount = 2;
				int count = 0;
				do {
					// find free ports
					int[] ports = TestUtil.GetFreePortToListen(address, 2);
					proxyEndPoint = new IPEndPoint(address, ports[0]);
					directEndPoint = new IPEndPoint(address, ports[1]);

					// try to start
					// Note that exit code 2 means 'endpoint conflicted'
					process = startServer(info, proxyEndPoint, directEndPoint);
					if (process.HasExited == false) {
						// succeeded
						this.ProxyEndPoint = proxyEndPoint;
						this.DirectEndPoint = directEndPoint;
						break;
					} else if (process.ExitCode != 2 || retryCount <= count) {
						break;
					}

					// prepare to retry
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
