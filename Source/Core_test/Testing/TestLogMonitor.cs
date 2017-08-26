using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Xunit;
using MAPE.Utils;


namespace MAPE.Testing {
	public class TestLogMonitor: ILogMonitor {
		#region data - synchronized by StateLocker 

		protected readonly object StateLocker = new object();

		private int flushingLogQueueTimeout = Timeout.Infinite;

		private bool logging = false;

		private bool enteringTestMode = false;

		public int TargetThreadId { get; private set; } = 0;

		private DateTime startTime = default(DateTime);

		private DateTime stopTime = default(DateTime);

		#endregion


		#region data - synchronized by entriesLocker 

		private readonly object entriesLocker = new object();

		private List<LogEntry> entries = new List<LogEntry>();

		#endregion


		#region properties

		public int FlushingLogQueueTimeout {
			get {
				return this.flushingLogQueueTimeout;
			}
			set {
				// argument checks
				if (value < 0 && value != Timeout.Infinite) {
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				this.flushingLogQueueTimeout = value;
			}
		}

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

		#endregion


		#region creation

		public TestLogMonitor() {
		}

		#endregion


		#region methods

		public void StartLogging(int targetThreadId, bool suppressTestMode = false) {
			lock (this.StateLocker) {
				// state checks
				if (this.logging) {
					throw new InvalidOperationException();
				}

				// start logging
				this.logging = true;
				try {
					if (suppressTestMode == false) {
						// set LogLevel to Verbose temporarily 
						Logger.EnterTestMode();
						this.enteringTestMode = true;
					}
					this.TargetThreadId = targetThreadId;
					this.startTime = DateTime.Now;
					this.stopTime = default(DateTime);

					Logger.AddLogMonitor(this);
				} catch {
					this.TargetThreadId = 0;
					if (this.enteringTestMode) {
						this.enteringTestMode = false;
						Logger.LeaveTestMode();
					}
					this.logging = false;
					throw;
				}
			}

			return;
		}

		public void StartLogging(bool filterByCurrentThread = false, bool suppressTestMode = false) {
			int targetThreadId = 0;
			if (filterByCurrentThread) {
				targetThreadId = Thread.CurrentThread.ManagedThreadId;
				Debug.Assert(targetThreadId != 0);
			}

			StartLogging(targetThreadId, suppressTestMode);
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
				Debug.Assert(this.stopTime == default(DateTime));
				this.stopTime = DateTime.Now;
				if (this.enteringTestMode) {
					this.enteringTestMode = false;
					Logger.LeaveTestMode();
				}
				this.TargetThreadId = 0;
				// Clear this.logging after Logger.RemoveLogMonitor() call,
				// otherwise queuing logs are lost.
				this.logging = false;
			}

			return;
		}

		public void ClearLogs() {
			lock (this.entriesLocker) {
				this.entries.Clear();
			}

			return;
		}

		/// <remark>
		/// Note this method locks the entry list while 
		/// it calls callback for every entry.
		/// </remark>
		public bool Iterate(Func<LogEntry, bool> callback) {
			// argument checks
			if (callback == null) {
				throw new ArgumentNullException(nameof(callback));
			}

			// iterate all entries
			lock (this.entriesLocker) {
				foreach (LogEntry entry in this.entries) {
					if (callback(entry)) {
						return true;	// found
					}
				}
			}

			return false;	// not found
		}

		public bool EqualLogEntry(LogEntry expected, LogEntry actual) {
			if (expected.EqualsExceptTimeAndThreadId(actual)) {
				DateTime time = actual.Time;
				if (this.startTime <= time && time <= this.stopTime) {
					return true;
				}
			}

			return false;
		}

		public bool Contains(LogEntry expected) {
			Func<LogEntry, bool> checker = (actual) => {
				return EqualLogEntry(expected, actual);
			};

			return Iterate(checker);
		}

		public void AssertContains(LogEntry expected) {
			Assert.True(Contains(expected), $"The expected log \"{expected.Message}\" is not monitored.");
		}

		#endregion


		#region ILogMonitor

		public void OnLog(LogEntry entry) {
			// state checks
			if (this.logging == false) {
				throw new InvalidOperationException("This log monitor is not logging now.");
			}
			if (this.TargetThreadId != 0 && entry.ThreadId != this.TargetThreadId) {
				// not target
				return;
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
