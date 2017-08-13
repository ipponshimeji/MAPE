using System;
using System.Diagnostics;
using System.Threading;


namespace MAPE.Utils {
	public struct LogEntry {
		#region constants

		public const int NoComponent = -1;

		public const int DefaultEventId = 0;

		#endregion


		#region data

		public readonly DateTime Time;

		public readonly int ParentComponentId;

		public readonly int ComponentId;

		public readonly string ComponentName;

		public readonly TraceEventType EventType;

		public readonly string Message;

		public readonly int EventId;

		public readonly int ThreadId;

		#endregion


		#region creation and disposal

		public LogEntry(DateTime time, int parentComponentId, int componentId, string componentName, TraceEventType eventType, string message, int eventId = DefaultEventId) {
			// argument checks
			if (parentComponentId < -1) {
				throw new ArgumentOutOfRangeException(nameof(parentComponentId));
			}
			if (componentId < -1) {
				throw new ArgumentOutOfRangeException(nameof(componentId));
			}
			// componentName can be null
			if (message == null) {
				message = string.Empty;
			}

			// initialize members
			this.Time = time;
			this.ParentComponentId = parentComponentId;
			this.ComponentId = componentId;
			this.ComponentName = componentName;
			this.EventType = eventType;
			this.Message = message;
			this.EventId = eventId;
			this.ThreadId = Thread.CurrentThread.ManagedThreadId;

			return;
		}

		public LogEntry(int parentComponentId, int componentId, string componentName, TraceEventType eventType, string message, int eventId): this(DateTime.Now, parentComponentId, componentId, componentName, eventType, message, eventId = DefaultEventId) {
		}

		public LogEntry(string componentName, TraceEventType eventType, string message, int eventId = DefaultEventId) : this(DateTime.Now, NoComponent, NoComponent, componentName, eventType, message, eventId) {
		}

		#endregion


		#region operators

		public static bool operator == (LogEntry x, LogEntry y) {
			return (
				EqualsExceptTimeAndThreadId(x, y) &&
				x.EventId == y.EventId &&
				x.Time == y.Time
			);
		}

		public static bool operator !=(LogEntry x, LogEntry y) {
			return !(x == y);
		}

		#endregion


		#region methods

		public static bool EqualsExceptTimeAndThreadId(LogEntry x, LogEntry y) {
			return (
				x.ParentComponentId == y.ParentComponentId &&
				x.ComponentId == y.ComponentId &&
				x.EventType == y.EventType &&
				x.EventId == y.EventId &&
				string.CompareOrdinal(x.ComponentName, y.ComponentName) == 0 &&
				string.CompareOrdinal(x.Message, y.Message) == 0
			);
		}

		public bool EqualsExceptTimeAndThreadId(LogEntry another) {
			return EqualsExceptTimeAndThreadId(this, another);
		}

		public static bool EqualsExceptTime(LogEntry x, LogEntry y) {
			return EqualsExceptTimeAndThreadId(x, y) && x.ThreadId == y.ThreadId;
		}

		public bool EqualsExceptTime(LogEntry another) {
			return EqualsExceptTime(this, another);
		}

		#endregion


		#region overrides

		public override bool Equals(object obj) {
			return (obj is LogEntry) ? this == (LogEntry)obj : false;
		}

		public override int GetHashCode() {
			// It is enough to count in Time and Message.
			return this.Time.GetHashCode() ^ this.Message.GetHashCode();
		}

		#endregion
	};
}
