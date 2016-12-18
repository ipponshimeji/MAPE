using System;
using System.Diagnostics;


namespace MAPE.Core {
	public abstract class Component: IDisposable {
		#region data

		/// <summary>
		/// 
		/// </summary>
		/// <remarks>
		/// ToDo: remarks for thread-safety
		/// </remarks>
		public string ObjectName {
			get;
			protected set;
		} = string.Empty;

		#endregion


		#region creation and disposal

		protected Component() {
		}

		public abstract void Dispose();

		#endregion


		#region methods - logging

		public string FormatTraceMessage(string message) {
			return string.Concat(DateTime.Now, " [", this.ObjectName, "] ", message);
		}

		[Conditional("TRACE")]
		public void TraceInformation(string message) {
			Trace.TraceInformation(FormatTraceMessage(message));
		}

		[Conditional("TRACE")]
		public void TraceWarning(string message) {
			Trace.TraceWarning(FormatTraceMessage(message));
		}

		[Conditional("TRACE")]
		public void TraceError(string message) {
			Trace.TraceError(FormatTraceMessage(message));
		}

		#endregion
	}
}
