using System;
using System.Diagnostics;
using MAPE.Utils;


namespace MAPE.ComponentBase {
	public abstract class Component: IDisposable, IComponentLogger {
		#region data

		public int ParentComponentId {
			get;
			protected set;
		} = LogEntry.NoComponent;

		public int ComponentId {
			get;
			protected set;
		} = LogEntry.NoComponent;

		/// <summary>
		/// 
		/// </summary>
		/// <remarks>
		/// ToDo: remarks for thread-safety
		/// </remarks>
		public string ComponentName {
			get;
			protected set;
		} = string.Empty;

		#endregion


		#region creation and disposal

		protected Component(bool allocateComponentId = true) {
			// initialize members
			if (allocateComponentId) {
				this.ComponentId = Logger.AllocComponentId();
			}

			return;
		}

		public abstract void Dispose();

		#endregion


		#region IComponentLogger

		public bool ShouldLog(TraceEventType eventType) {
			return Logger.ShouldLog(eventType);
		}

		public void Log(TraceEventType eventType, string message, int eventId) {
			Logger.Log(this.ParentComponentId, this.ComponentId, this.ComponentName, eventType, message, eventId);
		}

		public void Log(TraceEventType eventType, string message) {
			Logger.Log(this.ParentComponentId, this.ComponentId, this.ComponentName, eventType, message);
		}

		#endregion


		#region methods - logging

		public void LogCritical(string message, int eventId = LogEntry.DefaultEventId) {
			Log(TraceEventType.Critical, message, eventId);
		}

		public void LogError(string message, int eventId = LogEntry.DefaultEventId) {
			Log(TraceEventType.Error, message, eventId);
		}

		public void LogWarning(string message, int eventId = LogEntry.DefaultEventId) {
			Log(TraceEventType.Warning, message, eventId);
		}

		public void LogInformation(string message, int eventId = LogEntry.DefaultEventId) {
			Log(TraceEventType.Information, message, eventId);
		}

		public void LogVerbose(string message, int eventId = LogEntry.DefaultEventId) {
			Log(TraceEventType.Verbose, message, eventId);
		}

		public void LogStart(string message, int eventId = LogEntry.DefaultEventId) {
			Log(TraceEventType.Start, message, eventId);
		}

		public void LogStop(string message, int eventId = LogEntry.DefaultEventId) {
			Log(TraceEventType.Stop, message, eventId);
		}

		public void LogResume(string message, int eventId = LogEntry.DefaultEventId) {
			Log(TraceEventType.Resume, message, eventId);
		}

		public void LogSuspend(string message, int eventId = LogEntry.DefaultEventId) {
			Log(TraceEventType.Suspend, message, eventId);
		}

		#endregion


		#region methods - misc

		public ObjectDisposedException CreateObjectDisposedException() {
			return new ObjectDisposedException(this.ComponentName);
		}

		#endregion
	}
}
