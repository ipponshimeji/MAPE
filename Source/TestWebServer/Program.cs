using System;
using System.IO;
using System.Net;
using System.Threading;
using MAPE.Test.TestWeb;

namespace MAPE.Test.TestWebServer {
    class Program {
		// constants for exit code
		public const int SuccessExitCode = TestWebSettings.SuccessExitCode;
		public const int ErrorExitCode = TestWebSettings.ErrorExitCode;
		public const int EndPointInUseExitCode = TestWebSettings.EndPointInUseExitCode;

		// exit code
		private static int exitCode = SuccessExitCode;


		static int Main(string[] args) {
			// argument checks
			string httpPrefix = null;	// prefix as a http web server 
			string httpsPrefix = null;  // prefix as a https web server 
			string proxyPrefix = null;  // prefix as a proxy web server
			int argCount = args.Length;
			if (0 < argCount) {
				httpPrefix = args[0];
				if (1 < argCount) {
					httpsPrefix = args[1];
					if (2 < argCount) {
						proxyPrefix = args[2];
					}
				}
			}
			if (httpPrefix == null) {
				TestWebSettings.WriteWhetherServerIsStarted(false);
				WriteUsageTo(Console.Out);
				return ErrorExitCode;
			}

			// run web server
			// We create two web server: server and proxy.
			// In the HttpListener specification, we cannot detect whether a comming request is one to server or proxy
			// if those end points are listened in the same HttpListener object.
			// So we starts two HttpListeners.
			Server server = new Server(httpPrefix, httpsPrefix);
			Proxy proxy = new Proxy(proxyPrefix);
			try {
				// start the web server
				server.OnError += server_OnError;
				proxy.OnError += server_OnError;
				server.Start();
				proxy.Start();

				// run until a line received from the standard input
				TestWebSettings.WriteWhetherServerIsStarted(true);
				Console.Out.WriteLine("Hit Enter key to quit.");
				Console.In.ReadLine();
			} catch (Exception exception) {
				TestWebSettings.WriteWhetherServerIsStarted(false);
				ReportError(exception);
			} finally {
				proxy.Stop(Timeout.Infinite);
				server.Stop(Timeout.Infinite);
				proxy.OnError -= server_OnError;
				server.OnError -= server_OnError;
			}

			return Program.exitCode; 
        }

		private static void WriteUsageTo(TextWriter writer) {
			// show usage
			writer.WriteLine("USAGE:");
			writer.WriteLine("TestWebServer httpPrefix [proxyPrefix] [httpsPrefix]");
		}

		private static void WriteError(string message) {
			Console.Error.WriteLine(message);
		}

		private static void ReportError(Exception exception) {
			// show error message
			WriteError(exception.Message);
			Program.exitCode = ErrorExitCode;

			// handle special case
			HttpListenerException listenerException = exception as HttpListenerException;
			if (listenerException != null && listenerException.ErrorCode == 183) {
				// one of the specified end points was in use
				Program.exitCode = EndPointInUseExitCode;
			}
		}

		private static void server_OnError(object sender, ErrorEventArgs e) {
			if (e != null) {
				ReportError(e.GetException());
			}
		}
	}
}
