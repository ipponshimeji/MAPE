using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using MAPE.Test.TestWeb;


namespace MAPE.Testing.TestWebServerRunners {
	public class TestWebServerRunner: ObjectWithUseCount {
		#region data

		private readonly IPAddress addressForAutoPortsAllocation;

		#endregion


		#region data - synchronized by base.useCountLocker

		private Process process = null;

		public IPEndPoint HttpEndPoint { get; private set; }

		public IPEndPoint HttpsEndPoint { get; private set; }

		public IPEndPoint ProxyEndPoint { get; private set; }

		#endregion


		#region properties

		public bool AutoPortsAllocation {
			get {
				return this.addressForAutoPortsAllocation != null;
			}
		}

		public bool IsRunning {
			get {
				return this.process != null;
			}
		}

		#endregion


		#region creation and disposal

		public TestWebServerRunner(IPEndPoint httpEndPoint, IPEndPoint httpsEndPoint, IPEndPoint proxyEndPoint) {
			// argument checks
			if (httpEndPoint == null) {
				throw new ArgumentNullException(nameof(httpEndPoint));
			}
			// httpsEndPoint or proxyEndPoint can be null

			// initialize members
			this.addressForAutoPortsAllocation = null;
			this.HttpEndPoint = httpEndPoint;
			this.HttpsEndPoint = httpsEndPoint;
			this.ProxyEndPoint = proxyEndPoint;

			return;
		}

		public TestWebServerRunner(IPAddress address) {
			// argument checks
			if (address == null) {
				throw new ArgumentNullException(nameof(address));
			}

			// initialize members
			this.addressForAutoPortsAllocation = address;
			this.HttpEndPoint = null;
			this.HttpsEndPoint = null;
			this.ProxyEndPoint = null;

			return;
		}

		#endregion


		#region overrides

		protected override void OnUsed() {
			// state checks
			Process process = this.process;
			if (process != null) {
				return;
			}

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

			if (this.AutoPortsAllocation == false) {
				// start server with specified end points
				process = startServer(info, this.HttpEndPoint, this.HttpsEndPoint, this.ProxyEndPoint);
			} else {
				// start server with automatically allocated ports
				IPAddress address = this.addressForAutoPortsAllocation;
				Debug.Assert(address != null);
				int retryCount = 2;
				int count = 0;
				do {
					// find free ports
					int[] ports = TestUtil.GetFreePortToListen(address, 3);
					IPEndPoint httpEndPoint = new IPEndPoint(address, ports[0]);
					IPEndPoint httpsEndPoint = new IPEndPoint(address, ports[1]);
					IPEndPoint proxyEndPoint = new IPEndPoint(address, ports[2]);

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

		protected override void OnUnused() {
			// state checks
			Process process = this.process;
			this.process = null;
			if (process == null) {
				return;
			}

			if (this.AutoPortsAllocation) {
				this.HttpEndPoint = null;
				this.HttpsEndPoint = null;
				this.ProxyEndPoint = null;
			}
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


		#region privates

		private static string GetServerFilePath() {
			string dirPath = Path.GetDirectoryName(typeof(TestWebServerRunner).Assembly.ManifestModule.FullyQualifiedName);
			return Path.Combine(dirPath, "TestWebServer.dll");
		}

		#endregion
	}
}
