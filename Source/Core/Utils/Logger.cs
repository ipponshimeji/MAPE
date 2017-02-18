using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;


namespace MAPE.Utils {
	public static class Logger {
		#region constants

		public const string SourceName = "MAPE";

		public const string SourceSwitchName = "MAPESwitch";

		public const int NoComponent = -1;

		#endregion


		#region data

		public static readonly TraceSource Source = new TraceSource(SourceName);

		#endregion


		#region data synchronized by classLocker

		private static readonly object classLocker = new object();

		private static SourceLevels sourceLevels;

		private static TraceLevel logLevel;

		private static int nextComponentId = 0;

		private static bool loggingStopped = false;

		#endregion


		#region data synchronized by monitorsLocker

		private static readonly object monitorsLocker = new object();

		private static readonly List<ILogMonitor> monitors = new List<ILogMonitor>();

		#endregion


		#region data synchronized by deliveringLocker

		private static readonly object deliveringLocker = new object();

		private static readonly Queue<Log> logQueue = new Queue<Log>();

		private static Task deliveringTask = null;

		#endregion


		#region properties

		public static SourceLevels SourceLevels {
			get {
				return Logger.sourceLevels;
			}
		}

		public static TraceLevel LogLevel {
			get {
				return Logger.logLevel;
			}
			set {
				lock (Logger.classLocker) {
					if (Logger.logLevel != value && Logger.loggingStopped == false) {
						Logger.logLevel = value;
						Logger.sourceLevels = FromTraceLevel(Logger.sourceLevels, value);
					}
				}
			}
		}

		#endregion


		#region creation

		static Logger() {
			// initialize sourceLevels from the Switch of the TraceSource
			sourceLevels = Logger.Source.Switch.Level;

			// adjust Switch.Level to include Start/Stop event
			sourceLevels |= SourceLevels.ActivityTracing;

			logLevel = ToTraceLevel(sourceLevels);

			return;
		}

		#endregion


		#region methods - component id

		public static int AllocComponentId() {
			lock (classLocker) {
				if (Logger.nextComponentId == int.MaxValue) {
					throw new Exception("Internal resource overflow: no more component id to be allocated.");
				}
				return Logger.nextComponentId++;
			}
		}

		#endregion


		#region methods - log monitors

		public static void AddLogMonitor(ILogMonitor monitor) {
			// argument checks
			if (monitor == null) {
				throw new ArgumentNullException(nameof(monitor));
			}

			// add the monitor to the monitor list
			lock (Logger.monitorsLocker) {
				Logger.monitors.Add(monitor);
			}

			return;
		}

		public static bool RemoveLogMonitor(ILogMonitor monitor) {
			// argument checks
			// monitor can be null (but may not be found in the monitor list)

			// remove the monitor from the monitor list
			lock (Logger.monitorsLocker) {
				return Logger.monitors.Remove(monitor);
			}
		}

		#endregion


		#region methods - logging

		public static bool StopLogging(int millisecondsTimeout = 0) {
			bool stopConfirmed = false;
			try {
				// stop logging
				lock (Logger.classLocker) {
					Logger.LogLevel = TraceLevel.Off;
					Logger.loggingStopped = true;
				}

				// wait for the completion of the delivering task
				Task deliveringTask;
				lock (Logger.deliveringLocker) {
					deliveringTask = Logger.deliveringTask;
				}
				if (deliveringTask == null) {
					stopConfirmed = true;
				} else if (millisecondsTimeout != 0) {
					stopConfirmed = deliveringTask.Wait(millisecondsTimeout);
				}
			} catch (Exception exception) {
				TraceInternalError(null, $"Fail to stop logging system: {exception.Message}");
				// continue
			}

			return stopConfirmed;
		}


		public static bool ShouldLog(TraceEventType eventType) {
			// Note that flags in SourceLevels and TraceEventType are corresponding.
			return ((int)Logger.sourceLevels & (int)eventType) != 0;
		}

		public static void Log(Log log) {
			if (ShouldLog(log.EventType)) {
				EnqueueLog(log);
			}
		}

		public static void Log(int parentComponentId, int componentId, string componentName, TraceEventType eventType, string message, int eventId) {
			if (ShouldLog(eventType)) {
				EnqueueLog(new Log(parentComponentId, componentId, componentName, eventType, message, eventId));
			}
		}


		// general logging

		public static void LogCritical(string componentName, string message, int eventId = 0) {
			if (ShouldLog(TraceEventType.Critical)) {
				EnqueueLog(new Log(NoComponent, NoComponent, componentName, TraceEventType.Critical, message, eventId));
			}
		}

		public static void LogError(string componentName, string message, int eventId = 0) {
			if (ShouldLog(TraceEventType.Error)) {
				EnqueueLog(new Log(NoComponent, NoComponent, componentName, TraceEventType.Error, message, eventId));
			}
		}

		public static void LogWarning(string componentName, string message, int eventId = 0) {
			if (ShouldLog(TraceEventType.Warning)) {
				EnqueueLog(new Log(NoComponent, NoComponent, componentName, TraceEventType.Warning, message, eventId));
			}
		}

		public static void LogInformation(string componentName, string message, int eventId = 0) {
			if (ShouldLog(TraceEventType.Information)) {
				EnqueueLog(new Log(NoComponent, NoComponent, componentName, TraceEventType.Information, message, eventId));
			}
		}

		public static void LogVerbose(string componentName, string message, int eventId = 0) {
			if (ShouldLog(TraceEventType.Verbose)) {
				EnqueueLog(new Log(NoComponent, NoComponent, componentName, TraceEventType.Verbose, message, eventId));
			}
		}

		#endregion


		#region private

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sourceLevels"></param>
		/// <returns></returns>
		/// <remarks>
		/// Note that there is no precise mapping from SourceLevels to TraceLevel
		/// because SourceLevels is bit flags while TraceLevel is not.
		/// For example, (SourceLevels)0x40, that is "only Warning events through",
		/// is converted TraceLevel.Warning: "Critical, Error and Warning events through".
		/// But such tricky flags may not be used actuallly.
		/// </remarks>
		private static TraceLevel ToTraceLevel(SourceLevels sourceLevels) {
			// Note that members of SourceLevels here is a complex bit flags. 
			if ((sourceLevels & (SourceLevels.Verbose & ~SourceLevels.Information)) != 0) {
				return TraceLevel.Verbose;
			} else if ((sourceLevels & (SourceLevels.Information & ~SourceLevels.Warning)) != 0) {
				return TraceLevel.Info;
			} else if ((sourceLevels & (SourceLevels.Warning & ~SourceLevels.Error)) != 0) {
				return TraceLevel.Warning;
			} else if ((sourceLevels & SourceLevels.Error) != 0) {
				return TraceLevel.Error;
			}

			return TraceLevel.Off;
		}

		private static SourceLevels FromTraceLevel(SourceLevels currentSourceLevels, TraceLevel traceLevel) {
			SourceLevels newSourceLevels;

			// convert the traceLevel to a SourceLevels 
			switch (traceLevel) {
				case TraceLevel.Off:
					newSourceLevels = SourceLevels.Off;
					break;
				case TraceLevel.Error:
					newSourceLevels = SourceLevels.Error;
					break;
				case TraceLevel.Warning:
					newSourceLevels = SourceLevels.Warning;
					break;
				case TraceLevel.Info:
					newSourceLevels = SourceLevels.Information;
					break;
				default:
					// includes TraceLevel.Verbose
					newSourceLevels = SourceLevels.Verbose;
					break;
			}

			// do not change the SourceLevels.ActivityTracing bits
			Debug.Assert((newSourceLevels & SourceLevels.ActivityTracing) == 0);
			newSourceLevels |= (currentSourceLevels & SourceLevels.ActivityTracing);

			return newSourceLevels;
		}


		private static void EnqueueLog(Log log) {
			// argument checks
			Debug.Assert(ShouldLog(log.EventType));

			// queue the log
			lock (Logger.deliveringLocker) {
				// queue the log
				Logger.logQueue.Enqueue(log);

				// if the delivering task is not working, create it
				if (Logger.deliveringTask == null) {
					Logger.deliveringTask = Task.Run((Action)DeliverLogs);
				}
			}

			return;
		}

		private static void DeliverLogs() {
			// deliver logs
			int count = 0;
			do {
				Log log;

				// get a log from the queue
				lock (Logger.deliveringLocker) {
					if (Logger.logQueue.Count <= 0) {
						Logger.deliveringTask = null;
						break;
					}

					log = Logger.logQueue.Dequeue();
				}

				// process the log
				DeliverToTraceListeners(log);
				DeliverToLogMonitors(log);
				++count;
			} while (true);

			// log
			if (Logger.ShouldLog(TraceEventType.Verbose)) {
				TraceInternalVerbose(null, $"This delivering thread is quitting after delivered '{count}' log(s).");
			}

			return;
		}

		private static void DeliverToTraceListeners(Log log) {
			try {
				// adjust message
				string message;
				string componentName = log.ComponentName;
				if (string.IsNullOrEmpty(componentName)) {
					message = $"{log.Time} {log.Message}";
				} else {
					message = $"{log.Time} [{componentName}] {log.Message}";
				}

				// trace the log
				Logger.Source.TraceEvent(log.EventType, log.EventId, message);
			} catch (Exception exception) {
				TraceInternalError(null, $"An exception on calling TraceSource.TraceEvent(): {exception.Message}");
				// continue
			}
		}

		private static void DeliverToLogMonitors(Log log) {
			lock (Logger.monitorsLocker) {
				if (0 < Logger.monitors.Count) {
					Logger.monitors.ForEach(
						(monitor) => {
							try {
								monitor.OnLog(log);
							} catch (Exception exception) {
								TraceInternalError(null, $"An exception on calling ILogMonitor.OnLog(): {exception.Message}");
								// continue
							}
						}
					);	
				}
			}
		}


		private static void TraceInternal(TraceEventType eventType, string methodName, string message, int eventId = 0) {
			// argument checks
			if (string.IsNullOrEmpty(methodName) == false) {
				message = $"Logger: at {methodName}(), {message}";
			} else {
				message = $"Logger: {message}";
			}

			// trace the message
			Logger.Source.TraceEvent(eventType, 0, message);
		}

		private static void TraceInternalError(string methodName, string message, int eventId = 0) {
			TraceInternal(TraceEventType.Error, methodName, message, eventId);
		}

		private static void TraceInternalVerbose(string methodName, string message, int eventId = 0) {
			TraceInternal(TraceEventType.Verbose, methodName, message, eventId);
		}

		#endregion
	}
}
