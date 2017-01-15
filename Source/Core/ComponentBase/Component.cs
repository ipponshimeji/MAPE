using System;
using System.Diagnostics;
using MAPE.Utils;


namespace MAPE.ComponentBase {
	public abstract class Component: IDisposable, IComponentLogger {
		#region data

		public int ParentComponentId {
			get;
			protected set;
		} = Logger.NoComponent;

		public int ComponentId {
			get;
			protected set;
		} = Logger.NoComponent;

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

		public void Log(TraceEventType eventType, string message, int eventId = 0) {
			Logger.Log(this.ParentComponentId, this.ComponentId, this.ComponentName, eventType, message, eventId);
		}

		#endregion


		#region methods - logging

		public void LogCritical(string message, int eventId = 0) {
			Log(TraceEventType.Critical, message, eventId);
		}

		public void LogError(string message, int eventId = 0) {
			Log(TraceEventType.Error, message, eventId);
		}

		public void LogWarning(string message, int eventId = 0) {
			Log(TraceEventType.Warning, message, eventId);
		}

		public void LogInformation(string message, int eventId = 0) {
			Log(TraceEventType.Information, message, eventId);
		}

		public void LogVerbose(string message, int eventId = 0) {
			Log(TraceEventType.Verbose, message, eventId);
		}

		public void LogStart(string message, int eventId = 0) {
			Log(TraceEventType.Start, message, eventId);
		}

		public void LogStop(string message, int eventId = 0) {
			Log(TraceEventType.Stop, message, eventId);
		}

		#endregion


		#region methods - settings

		public Settings GetSettings(bool omitDefault) {
			Settings settings = Settings.CreateEmptySettings();

			// add settings of each class level
			lock (this) {
				AddSettings(settings, omitDefault);
			}

			return settings;
		}

		#endregion


		#region methods - misc

		public ObjectDisposedException CreateObjectDisposedException() {
			return new ObjectDisposedException(this.ComponentName);
		}

		#endregion


		#region overridables

		public virtual void AddSettings(Settings settings, bool omitDefault) {
			// not supported by default
			throw new NotSupportedException();
		}

		#endregion
	}
}
