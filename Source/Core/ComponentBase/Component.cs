using System;
using System.Diagnostics;
using MAPE.Utils;


namespace MAPE.ComponentBase {
	public abstract class Component: IDisposable, ILogger {
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


		#region ILogger

		public bool IsLogged(TraceEventType eventType) {
			return Logger.IsLogged(eventType);
		}

		public void LogCritical(string message) {
			Logger.LogCritical(FormatTraceMessage(message));
		}

		public void LogError(string message) {
			Logger.LogError(FormatTraceMessage(message));
		}

		public void LogWarning(string message) {
			Logger.LogWarning(FormatTraceMessage(message));
		}

		public void LogInformation(string message) {
			Logger.LogInformation(FormatTraceMessage(message));
		}

		public void LogVerbose(string message) {
			Logger.LogVerbose(FormatTraceMessage(message));
		}

		public void LogStart(string message) {
			Logger.LogStart(FormatTraceMessage(message));
		}

		public void LogStop(string message) {
			Logger.LogStop(FormatTraceMessage(message));
		}

		#endregion


		#region methods - logging

		public string FormatTraceMessage(string message) {
			return string.Concat(DateTime.Now, " [", this.ObjectName, "] ", message);
		}

		#endregion
	}
}
