using System;
using System.Diagnostics;


namespace MAPE.Utils {
	public struct Log {
		#region data

		public readonly DateTime Time;

		public readonly int ParentComponentId;

		public readonly int ComponentId;

		public readonly string ComponentName;

		public readonly TraceEventType EventType;

		public readonly string Message;

		public readonly int EventId;

		#endregion


		#region creation and disposal

		public Log(DateTime time, int parentComponentId, int componentId, string componentName, TraceEventType eventType, string message, int eventId) {
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

			return;
		}

		public Log(int parentComponentId, int componentId, string componentName, TraceEventType eventType, string message, int eventId): this(DateTime.Now, parentComponentId, componentId, componentName, eventType, message, eventId) {
		}

		#endregion
	};
}
