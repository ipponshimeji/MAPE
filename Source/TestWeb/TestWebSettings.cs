using System;
using System.Diagnostics;

namespace MAPE.Test.TestWeb {
    public class TestWebSettings {
		#region constants

		// TestWebServer exit codes
		public const int SuccessExitCode = 0;
		public const int ErrorExitCode = 1;
		public const int EndPointInUseExitCode = 2;

		// status message which TestWebServer outputs
		public const string SucceededStatusMessage = "Started.";
		public const string FailedStatusMessage = "Failed.";

		#endregion


		#region methods

		public static void WriteWhetherServerIsStarted(bool succeeded) {
			Console.Error.WriteLine(succeeded ? SucceededStatusMessage : FailedStatusMessage);
		}

		public static bool ReadWhetherServerIsStarted(Process serverProcess) {
			string status = serverProcess.StandardError.ReadLine();
			return status == SucceededStatusMessage;
		}

		#endregion
	}
}
