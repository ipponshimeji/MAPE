using System;
using System.IO;
using System.Net;
using System.Threading;

namespace MAPE.Test.TestWebServer {
    class Program {
		public const int SuccessExitCode = 0;
		public const int ErrorExitCode = 1;
		public const int EndPointInUseExitCode = 2;

		private static int exitCode = SuccessExitCode;

		static int Main(string[] args) {
			// argument checks
			string proxyPrefix = null;  // prefix as a proxy style web server
			string directPrefix = null; // prefix as a direct style web server 
			int argCount = args.Length;
			if (0 < argCount) {
				proxyPrefix = args[0];
				if (1 < argCount) {
					directPrefix = args[1];
				}
			}
			if (proxyPrefix == null) {
				ShowUsage(Console.Out);
				return ErrorExitCode;
			}

			// run web server
			Server server = new Server(proxyPrefix, directPrefix);
			try {
				// start the web server
				server.OnError += server_OnError;
				server.Start();

				// run until a line received from the standard input
				WriteStatusOutput("Started.");
				Console.Out.WriteLine("Hit Enter key to quit.");
				Console.In.ReadLine();
			} catch (Exception exception) {
				ReportError(exception);
				WriteStatusOutput("Failed.");
			} finally {
				server.Stop(Timeout.Infinite);
				server.OnError -= server_OnError;
			}

			return Program.exitCode; 
        }

		private static void ShowUsage(TextWriter writer) {
			writer.WriteLine("USAGE:");
			writer.WriteLine("TestWebServer proxyPrefix [directPrefix]");
		}

		private static void ReportError(Exception exception) {
			Console.Error.WriteLine(exception.Message);
			Program.exitCode = ErrorExitCode;

			// handle special case
			HttpListenerException listenerException = exception as HttpListenerException;
			if (listenerException != null && listenerException.ErrorCode == 183) {
				// one of the specified end points is in use
				Program.exitCode = EndPointInUseExitCode;
			}
		}

		// Note that an invoker use this output to detect whether the server starts successfully.
		// See MAPE.Testing.TestWebServer.Start()
		private static void WriteStatusOutput(string line) {
			Console.Out.WriteLine(line);
		}

		private static void server_OnError(object sender, ErrorEventArgs e) {
			if (e != null) {
				ReportError(e.GetException());
			}
		}
	}
}
