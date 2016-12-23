using System;
using System.Diagnostics;


namespace MAPE.Utils {
	public class ColorConsoleTraceListener: ConsoleTraceListener {
		#region creation and disposal

		public ColorConsoleTraceListener(bool useErrorStream) : base(useErrorStream) {
		}

		#endregion


		#region methods

		public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format) {
			TraceEvent(eventCache, source, eventType, id, format, null);
		}

		public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args) {
			// decide color
			ConsoleColor currentColor = Console.ForegroundColor;
			ConsoleColor color;
			switch (eventType) {
				case TraceEventType.Critical:
				case TraceEventType.Error:
					color = ConsoleColor.Red;
					break;
				case TraceEventType.Warning:
					color = ConsoleColor.Magenta;
					break;
				case TraceEventType.Information:
					color = ConsoleColor.Green;
					break;
				case TraceEventType.Verbose:
					color = ConsoleColor.Gray;
					break;
				default:
					color = currentColor;
					break;
			}

			if (color == currentColor) {
				base.TraceEvent(eventCache, source, eventType, id, format);
			} else {
				Console.ForegroundColor = color;
				try {
					base.TraceEvent(eventCache, source, eventType, id, format);
				} finally {
					Console.ForegroundColor = currentColor;
				}
			}

			return;
		}

		#endregion
	}
}
