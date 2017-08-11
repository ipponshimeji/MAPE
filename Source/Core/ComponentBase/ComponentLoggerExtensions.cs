using System;
using System.Diagnostics;
using MAPE.Utils;


namespace MAPE.ComponentBase {
	public static class ComponentLoggerExtensions {
		#region methods

		public static void LogCritical(this IComponentLogger logger, string message, int eventId = LogEntry.DefaultEventId) {
			// argument checks
			if (logger == null) {
				throw new ArgumentNullException(nameof(logger));
			}

			logger.Log(TraceEventType.Critical, message, eventId);
		}

		public static void LogError(this IComponentLogger logger, string message, int eventId = LogEntry.DefaultEventId) {
			// argument checks
			if (logger == null) {
				throw new ArgumentNullException(nameof(logger));
			}

			logger.Log(TraceEventType.Error, message, eventId);
		}

		public static void LogWarning(this IComponentLogger logger, string message, int eventId = LogEntry.DefaultEventId) {
			// argument checks
			if (logger == null) {
				throw new ArgumentNullException(nameof(logger));
			}

			logger.Log(TraceEventType.Warning, message, eventId);
		}

		public static void LogInformation(this IComponentLogger logger, string message, int eventId = LogEntry.DefaultEventId) {
			// argument checks
			if (logger == null) {
				throw new ArgumentNullException(nameof(logger));
			}

			logger.Log(TraceEventType.Information, message, eventId);
		}

		public static void LogVerbose(this IComponentLogger logger, string message, int eventId = LogEntry.DefaultEventId) {
			// argument checks
			if (logger == null) {
				throw new ArgumentNullException(nameof(logger));
			}

			logger.Log(TraceEventType.Verbose, message, eventId);
		}

		public static void LogStart(this IComponentLogger logger, string message, int eventId = LogEntry.DefaultEventId) {
			// argument checks
			if (logger == null) {
				throw new ArgumentNullException(nameof(logger));
			}

			logger.Log(TraceEventType.Start, message, eventId);
		}

		public static void LogStop(this IComponentLogger logger, string message, int eventId = LogEntry.DefaultEventId) {
			// argument checks
			if (logger == null) {
				throw new ArgumentNullException(nameof(logger));
			}

			logger.Log(TraceEventType.Stop, message, eventId);
		}

		#endregion
	}
}
