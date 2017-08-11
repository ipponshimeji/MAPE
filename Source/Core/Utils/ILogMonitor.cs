using System;
using System.Diagnostics;


namespace MAPE.Utils {
	public interface ILogMonitor {
		void OnLog(LogEntry entry);
	}
}
