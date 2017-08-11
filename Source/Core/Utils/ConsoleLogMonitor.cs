using System;
using System.Diagnostics;


namespace MAPE.Utils {
	public class ConsoleLogMonitor: ILogMonitor {
		#region creation and disposal

		public ConsoleLogMonitor() {
		}

		#endregion


		#region ILogMonitor

		public virtual void OnLog(LogEntry entry) {
			Console.WriteLine(GetLogMessage(entry));
		}

		#endregion


		#region overrides

		protected virtual string GetLogMessage(LogEntry entry) {
			return $"{entry.Time.ToString("T")}, {GetEventTypeName(entry)}, {entry.ComponentName}: {entry.Message}";
		}

		#endregion


		#region privates

		private string GetEventTypeName(LogEntry entry) {
			TraceEventType eventType = entry.EventType;
			return (eventType == TraceEventType.Information) ? "Info" : eventType.ToString();
		}

		#endregion
	}
}
