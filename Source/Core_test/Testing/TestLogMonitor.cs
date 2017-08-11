using System;
using System.Collections.Generic;
using System.Diagnostics;
using MAPE.Utils;


namespace MAPE.Test.Testing {
	public class TestLogMonitor: ILogMonitor {
		#region data

		protected readonly object StateLocker = new object();

		private readonly object entriesLocker = new object();

		#endregion


		#region data - synchronized by StateLocker 

		public int FlushingLogQueueTimeout { get; set; } = 1000;

		private bool logging = false;

		private TraceLevel logLevelBackup = TraceLevel.Error;

		private DateTime startTime = default(DateTime);

		private DateTime stopTime = default(DateTime);

		#endregion


		#region data - synchronized by entriesLocker 

		private List<LogEntry> entries = new List<LogEntry>();

		#endregion


		#region properties

		public IReadOnlyList<LogEntry> Entries {
			get {
				return this.entries;
			}
		}

		public int LogCount {
			get {
				return this.entries.Count;
			}
		}

		public LogEntry FirstLog {
			get {
				return (0 < this.entries.Count)? this.entries[0]: default(LogEntry);
			}
		}

		#endregion


		#region creation

		public TestLogMonitor() {
		}

		#endregion


		#region methods

		public void StartLogging(TraceLevel? logLevel = TraceLevel.Verbose) {
			lock (this.StateLocker) {
				// state checks
				if (this.logging) {
					throw new InvalidOperationException();
				}

				// start logging
				this.logging = true;
				try {
					this.logLevelBackup = Logger.LogLevel;
					if (logLevel != null) {
						Logger.LogLevel = logLevel.Value;
					}
					try {
						this.startTime = DateTime.Now;
						this.stopTime = default(DateTime);

						Logger.AddLogMonitor(this);
					} catch {
						Logger.LogLevel = this.logLevelBackup;
						throw;
					}
				} catch {
					this.logging = false;
					throw;
				}
			}


			return;
		}

		public void StopLogging() {
			lock (this.StateLocker) {
				// state checks
				if (this.logging == false) {
					throw new InvalidOperationException();
				}

				// stop logging
				// specify flushingQueueTimeout to ensure the monitored logs are delivered,
				// because log delivery is asynchronous.
				Logger.RemoveLogMonitor(this, flushingQueueTimeout: this.FlushingLogQueueTimeout);
				Logger.LogLevel = this.logLevelBackup;
				// Clear this.logging after Logger.RemoveLogMonitor() call,
				// otherwise queuing logs are lost.
				this.logging = false;

				Debug.Assert(this.stopTime == default(DateTime));
				this.stopTime = DateTime.Now;
			}

			return;
		}

		public void AssertEqualLog(LogEntry expected, LogEntry actual) {
			TestUtil.AssertEqualLog(expected, this.startTime, this.stopTime, actual);
		}

		public void AssertEqualLog(LogEntry expected, int actualIndex) {
			// argument checks
			if (actualIndex < 0 || this.entries.Count <= actualIndex) {
				throw new ArgumentOutOfRangeException(nameof(actualIndex));
			}

			AssertEqualLog(expected, this.entries[actualIndex]);
		}

		#endregion


		#region ILogMonitor

		public void OnLog(LogEntry entry) {
			// state checks
			if (this.logging == false) {
				throw new InvalidOperationException("This log monitor is not logging now.");
			}

			// Not to lock this.StateLocker.
			// That may be cause deadlock in StopLogging() to wait for flushing queue.
			lock (this.entriesLocker) {
				this.entries.Add(entry);
			}
		}

		#endregion
	}
}
