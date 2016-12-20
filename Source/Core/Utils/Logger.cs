using System;
using System.Diagnostics;


namespace MAPE.Utils {
	public static class Logger {
		#region methods - logging

		[Conditional("TRACE")]
		public static void TraceInformation(string message) {
			Trace.TraceInformation(message);
		}

		[Conditional("TRACE")]
		public static void TraceWarning(string message) {
			Trace.TraceWarning(message);
		}

		[Conditional("TRACE")]
		public static void TraceError(string message) {
			Trace.TraceError(message);
		}

		#endregion
	}
}
