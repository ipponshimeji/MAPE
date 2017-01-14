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

		public static readonly SourceSwitch Switch = new SourceSwitch(SourceSwitchName);

		public static readonly TraceSource Source = new TraceSource(SourceName);

		#endregion


		#region data synchronized by classLocker

		private static readonly object classLocker = new object();

		private static SourceLevels sourceLevelsCache;

		private static TraceLevel logLevel;

		private static int nextComponentId = 0;

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
				return Logger.sourceLevelsCache;
			}
		}

		public static TraceLevel LogLevel {
			get {
				return Logger.logLevel;
			}
			set {
				lock (Logger.classLocker) {
					if (Logger.logLevel != value) {
						Logger.logLevel = value;
						Logger.sourceLevelsCache = FromTraceLevel(Logger.Switch.Level, value);
						Logger.Switch.Level = Logger.sourceLevelsCache;
					}
				}
			}
		}

		#endregion


		#region creation

		static Logger() {
			// initialize members
			sourceLevelsCache = Switch.Level;
			logLevel = ToTraceLevel(Switch.Level);

			return;
		}

		#endregion


		#region methods - component id

		public static int AllocComponentId() {
			lock (classLocker) {
				// ToDo: how handle overflow? ignore?
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

			// add the monitor
			lock (Logger.monitorsLocker) {
				Logger.monitors.Add(monitor);
			}

			return;
		}

		public static bool RemoveLogMonitor(ILogMonitor monitor) {
			// argument checks
			if (monitor == null) {
				throw new ArgumentNullException(nameof(monitor));
			}

			// remove the monitor
			lock (Logger.monitorsLocker) {
				return Logger.monitors.Remove(monitor);
			}
		}

		#endregion


		#region methods - logging

		public static bool ShouldLog(TraceEventType eventType) {
			// Note that flags in SourceLevels and TraceEventType are corresponding.
			return ((int)Logger.sourceLevelsCache & (int)eventType) != 0;
		}

		public static void Log(Log log) {
			if (ShouldLog(log.EventType)) {
				LogInternal(log);
			}
		}

		public static void Log(int parentComponentId, int componentId, string componentName, TraceEventType eventType, string message, int eventId) {
			if (ShouldLog(eventType)) {
				LogInternal(new Log(parentComponentId, componentId, componentName, eventType, message, eventId));
			}
		}


		// general logging

		public static void LogCritical(string componentName, string message, int eventId = 0) {
			if (ShouldLog(TraceEventType.Critical)) {
				LogInternal(new Log(NoComponent, NoComponent, componentName, TraceEventType.Critical, message, eventId));
			}
		}

		public static void LogError(string componentName, string message, int eventId = 0) {
			if (ShouldLog(TraceEventType.Error)) {
				LogInternal(new Log(NoComponent, NoComponent, componentName, TraceEventType.Error, message, eventId));
			}
		}

		public static void LogWarning(string componentName, string message, int eventId = 0) {
			if (ShouldLog(TraceEventType.Warning)) {
				LogInternal(new Log(NoComponent, NoComponent, componentName, TraceEventType.Warning, message, eventId));
			}
		}

		public static void LogInformation(string componentName, string message, int eventId = 0) {
			if (ShouldLog(TraceEventType.Information)) {
				LogInternal(new Log(NoComponent, NoComponent, componentName, TraceEventType.Information, message, eventId));
			}
		}

		public static void LogVerbose(string componentName, string message, int eventId = 0) {
			if (ShouldLog(TraceEventType.Verbose)) {
				LogInternal(new Log(NoComponent, NoComponent, componentName, TraceEventType.Verbose, message, eventId));
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


		private static void LogInternal(Log log) {
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
			do {
				Log log;

				// get a log from the queue
				lock (Logger.deliveringLocker) {
					if (Logger.logQueue.Count <= 0) {
						Logger.deliveringTask = null;
						return;
					}

					log = Logger.logQueue.Dequeue();
				}

				// process the log
				DeliverToTraceListeners(log);
				DeliverToLogMonitors(log);
			} while (true);
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
				LogLoggingError($"An exception on calling TraceSource.TraceEvent(): {exception.Message}");
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
								LogLoggingError($"An exception on calling ILogMonitor.OnLog(): {exception.Message}");
								// continue
							}
						}
					);	
				}
			}
		}

		private static void LogLoggingError(string message) {
			Logger.Source.TraceEvent(TraceEventType.Error, 0, $"Logging Error: {message}");
		}

		#endregion
	}
}
