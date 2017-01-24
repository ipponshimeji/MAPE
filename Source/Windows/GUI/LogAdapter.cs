using System;
using System.Diagnostics;
using MAPE.Utils;


namespace MAPE.Windows.GUI {
	public class LogAdapter {
		#region data

		private readonly Log log;

		#endregion


		#region properties

		public string Time {
			get {
				return this.log.Time.ToString("T");
			}
		}

		public string ComponentName {
			get {
				return this.log.ComponentName;
			}
		}

		public TraceEventType EventType {
			get {
				return this.log.EventType;
			}
		}

		public string EventTypeName {
			get {
				TraceEventType eventType = this.log.EventType;
				return (eventType == TraceEventType.Information) ? "Info" : eventType.ToString();
			}
		}

		public string Message {
			get {
				return this.log.Message;
			}
		}

		#endregion


		#region creation and disposal

		public LogAdapter(Log log) {
			// initialize members
			this.log = log;

			return;
		}

		#endregion
	}
}
