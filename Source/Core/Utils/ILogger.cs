using System;
using System.Diagnostics;


namespace MAPE.Utils {
	public interface ILogger {
		bool IsLogged(TraceEventType eventType);

		void LogCritical(string message);

		void LogError(string message);

		void LogWarning(string message);

		void LogInformation(string message);

		void LogVerbose(string message);

		void LogStart(string message);

		void LogStop(string message);
	}
}
