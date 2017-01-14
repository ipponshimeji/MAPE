using System;
using System.Diagnostics;


namespace MAPE.ComponentBase {
	public interface IComponentLogger {
		bool ShouldLog(TraceEventType eventType);

		void Log(TraceEventType eventType, string message, int eventId = 0);
	}
}
