using System;
using System.Diagnostics;


namespace MAPE.Utils {
	public class ColorConsoleLogMonitor: ConsoleLogMonitor {
		#region creation and disposal

		public ColorConsoleLogMonitor(): base() {
		}

		#endregion


		#region ILogMonitor

		public override void OnLog(LogEntry entry) {
			// decide color
			ConsoleColor currentColor = Console.ForegroundColor;
			ConsoleColor color;
			switch (entry.EventType) {
				case TraceEventType.Critical:
					color = ConsoleColor.Red;
					break;
				case TraceEventType.Error:
					color = ConsoleColor.Magenta;
					break;
				case TraceEventType.Warning:
					color = ConsoleColor.DarkYellow;
					break;
				case TraceEventType.Information:
					color = ConsoleColor.Green;
					break;
				case TraceEventType.Verbose:
				case TraceEventType.Start:
				case TraceEventType.Stop:
				case TraceEventType.Suspend:
				case TraceEventType.Resume:
					color = ConsoleColor.DarkGray;
					break;
				default:
					color = currentColor;
					break;
			}

			// output the entry
			if (color == currentColor) {
				base.OnLog(entry);
			} else {
				Console.ForegroundColor = color;
				try {
					base.OnLog(entry);
				} finally {
					Console.ForegroundColor = currentColor;
				}
			}

			return;
		}

		#endregion
	}
}
