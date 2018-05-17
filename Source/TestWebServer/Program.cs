using System;
using System.IO;
using System.Threading;

namespace MAPE.Test.TestWebServer {
    class Program {
		public const int SuccessExitCode = 0;
		public const int ErrorExitCode = 0;

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
				ShowUsage();
				return ErrorExitCode;
			}

			// run web server
			int exitCode = SuccessExitCode;
			Server server = new Server(proxyPrefix, directPrefix);
			try {
				// start the web server
				server.Start();

				// run until a line received from the standard input
				Console.Out.WriteLine("Hit Enter key to quit.");
				Console.In.ReadLine();
			} catch (Exception exception) {
				Console.Error.WriteLine(exception);
				exitCode = ErrorExitCode;
			} finally {
				server.Stop(Timeout.Infinite);
			}

			return exitCode; 
        }

		private static void ShowUsage() {
			TextWriter output = Console.Out;

			output.WriteLine("USAGE:");
			output.WriteLine("TestWebServer proxyPrefix [directPrefix]");
		}
	}
}
