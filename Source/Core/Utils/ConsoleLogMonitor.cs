using System;
using System.Diagnostics;


namespace MAPE.Utils {
	public class ConsoleLogMonitor: ILogMonitor {
		#region creation and disposal

		public ConsoleLogMonitor() {
		}

		#endregion


		#region ILogMonitor

		public virtual void OnLog(Log log) {
			Console.WriteLine(GetLogMessage(log));
		}

		#endregion


		#region overrides

		protected virtual string GetLogMessage(Log log) {
			return $"{log.Time.ToString("T")}, {GetEventTypeName(log)}, {log.ComponentName}: {log.Message}";
		}

		#endregion


		#region privates

		private string GetEventTypeName(Log log) {
			TraceEventType eventType = log.EventType;
			return (eventType == TraceEventType.Information) ? "Info" : eventType.ToString();
		}

		#endregion
	}
}
