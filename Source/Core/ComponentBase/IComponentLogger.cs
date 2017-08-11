using System;
using System.Diagnostics;
using MAPE.Utils;


namespace MAPE.ComponentBase {
	public interface IComponentLogger {
		bool ShouldLog(TraceEventType eventType);

		void Log(TraceEventType eventType, string message, int eventId);

		void Log(TraceEventType eventType, string message);
	}
}
