using System;
using System.IO;
using System.Threading;

namespace MAPE.Test.TestWebServer {
    class Program {
		public const int SuccessExitCode = 0;
		public const int ErrorExitCode = 1;

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
				Console.Out.WriteLine("Hit Enter key to quit.");
				Console.In.ReadLine();
			} catch (Exception exception) {
				ReportError(exception);
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
		}

		private static void server_OnError(object sender, ErrorEventArgs e) {
			if (e != null) {
				ReportError(e.GetException());
			}
		}
	}
}
