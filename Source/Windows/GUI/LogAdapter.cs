using System;
using System.Diagnostics;
using MAPE.Utils;


namespace MAPE.Windows.GUI {
	public class LogAdapter {
		#region data

		private readonly LogEntry entry;

		#endregion


		#region properties

		public string Time {
			get {
				return this.entry.Time.ToString("T");
			}
		}

		public string ComponentName {
			get {
				return this.entry.ComponentName;
			}
		}

		public TraceEventType EventType {
			get {
				return this.entry.EventType;
			}
		}

		public string EventTypeName {
			get {
				TraceEventType eventType = this.entry.EventType;
				return (eventType == TraceEventType.Information) ? "Info" : eventType.ToString();
			}
		}

		public string Message {
			get {
				return this.entry.Message;
			}
		}

		#endregion


		#region creation and disposal

		public LogAdapter(LogEntry entry) {
			// initialize members
			this.entry = entry;

			return;
		}

		#endregion
	}
}
