using System;
using System.Diagnostics;


namespace MAPE.Utils {
	public static class Logger {
		#region types

		// ToDo: default logger instance

		#endregion


		#region constants

		public const string SourceName = "MAPE";

		#endregion


		#region data synchronized by classLocker

		private static object classLocker = new object();

		private static SourceSwitch sourceSwitch = null;

		private static TraceSource traceSource = null;

		#endregion


		#region properties

		public static SourceSwitch SourceSwitch {
			get {
				SourceSwitch value = sourceSwitch;
				if (value == null) {
					lock (classLocker) {
						if (value == null) {
							value = new SourceSwitch("MAPESwitch");
							sourceSwitch = value;
						}
					}
				}

				return value;
			}
		}

		public static TraceSource Source {
			get {
				TraceSource value = traceSource;
				if (value == null) {
					lock (classLocker) {
						if (value == null) {
							value = new TraceSource(SourceName);
							value.Switch = SourceSwitch;
							traceSource = value;
						}
					}
				}

				return value;
			}
		}

		#endregion


		#region methods - logging

		public static bool IsLogged(TraceEventType eventType) {
			return ((int)SourceSwitch.Level & (int)eventType) != 0;
		}

		[Conditional("TRACE")]
		public static void LogCritical(string message) {
			Source.TraceEvent(TraceEventType.Critical, 0, message);
		}

		[Conditional("TRACE")]
		public static void LogError(string message) {
			Source.TraceEvent(TraceEventType.Error, 0, message);
		}

		[Conditional("TRACE")]
		public static void LogWarning(string message) {
			Source.TraceEvent(TraceEventType.Warning, 0, message);
		}

		[Conditional("TRACE")]
		public static void LogInformation(string message) {
			Source.TraceEvent(TraceEventType.Information, 0, message);
		}

		[Conditional("TRACE")]
		public static void LogVerbose(string message) {
			Source.TraceEvent(TraceEventType.Verbose, 0, message);
		}

		[Conditional("TRACE")]
		public static void LogStart(string message) {
			Source.TraceEvent(TraceEventType.Start, 0, message);
		}

		[Conditional("TRACE")]
		public static void LogStop(string message) {
			Source.TraceEvent(TraceEventType.Stop, 0, message);
		}

		#endregion
	}
}
