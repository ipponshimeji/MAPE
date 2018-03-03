using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MAPE.Utils;
using MAPE.Command.Settings;
using MAPE.Properties;


namespace MAPE.Command {
    public abstract class GUICommandBase: CommandBase {
		#region types

		public static new class OptionNames {
			#region constants

			public const string Start = "Start";

			#endregion
		}

		protected class Starter {
			#region data

			public readonly GUICommandBase Owner;

			public readonly bool Resuming;

			public readonly int TryCount;

			public readonly int Delaly;

			public readonly int Interval;

			#endregion


			#region data - data synchronized by instanceLocker

			private readonly object instanceLocker = new object();

			private Task task = null;

			private bool canceled = false;

			#endregion


			#region creation and disposal

			public Starter(GUICommandBase owner, bool resuming, int tryCount, int delay, int interval) {
				// argument checks
				if (owner == null) {
					throw new ArgumentNullException(nameof(owner));
				}
				if (tryCount <= 0) {
					throw new ArgumentOutOfRangeException(nameof(tryCount));
				}
				if (delay < 0) {
					throw new ArgumentOutOfRangeException(nameof(delay));
				}
				if (interval < 0) {
					throw new ArgumentOutOfRangeException(nameof(interval));
				}

				// initialize members
				this.Owner = owner;
				this.Resuming = resuming;
				this.TryCount = tryCount;
				this.Delaly = delay;
				this.Interval = interval;

				return;
			}

			#endregion


			#region methods

			public void Schedule() {
				Task task;
				lock (this.instanceLocker) {
					// state checks
					if (this.canceled) {
						return;
					}
					if (this.task != null) {
						// already scheduled
						return;
					}

					// create starting task
					task = new Task(this.TryToStart);
					this.task = task;
				}

				// start the starting task
				task.Start();

				return;
			}

			public void Cancel() {
				lock (this.instanceLocker) {
					this.canceled = true;
				}
			}

			#endregion


			#region privates

			private void TryToStart() {
				GUICommandBase owner = this.Owner;
				int counter = this.TryCount;
				string logMessage;

				// try to start proxying 
				logMessage = string.Format(Resources.GUICommandBase_Message_TryStarting, this.Delaly);
				owner.LogVerbose(logMessage);
				Thread.Sleep(this.Delaly);
				Debug.Assert(0 < counter);
				do {
					// check whether starting was canceled
					lock (this.instanceLocker) {
						if (this.canceled) {
							break;
						}
					}

					// try to start proxying
					try {
						owner.StartProxy(forScheduled: true);
						break;
					} catch {
						// continue;				
					}

					// prepare the next try
					--counter;
					if (counter <= 0) {
						// fail to resume
						owner.GiveUpScheduledStarting();
						owner.LogError(Resources.GUICommandBase_Message_FailToStart);
						break;
					}
					logMessage = string.Format(Resources.GUICommandBase_Message_RetryStarting, this.Interval);
					owner.LogError(logMessage);
					Thread.Sleep(this.Interval);
				} while (true);

				return;
			}

			#endregion
		}

		#endregion


		#region data

		private CommandSettings settings;

		private RunningProxyState runningProxyState = null;

		private Starter starter = null;

		#endregion


		#region events

		public event EventHandler ProxyStateChanged = null;

		#endregion


		#region properties

		public bool IsProxyRunning {
			get {
				return this.runningProxyState != null;
			}
		}

		public CommandSettings Settings {
			get {
				return this.settings;
			}
			protected set {
				if (this.IsProxyRunning) {
					throw new InvalidOperationException("The settings cannot be changed when the proxy is running.");
				}
				this.settings = value;
			}
		}

		#endregion


		#region creation and disposal

		public GUICommandBase(ComponentFactory componentFactory): base(componentFactory) {
		}

		public override void Dispose() {
			// dispose this class level
			GiveUpScheduledStarting();
			if (this.runningProxyState != null) {
				StopProxy(systemSessionEnding: false);
			}

			// dispose the base class level
			base.Dispose();
		}

		#endregion


		#region methods

		public void StartProxy(bool forScheduled = false) {
			// start proxy
			bool resuming = false;
			lock (this) {
				// state checks
				Starter starter = this.starter;
				if (forScheduled && starter == null) {
					// start only the scheduled proxying
					return;
				}
				if (this.runningProxyState != null) {
					ClearStarter();	// no use if proxying is running
					return;
				}
				if (starter != null) {
					resuming = starter.Resuming;
				}

				// start
				// Note that the starter won't be cleared if StartProxy() throws an exception. 
				this.runningProxyState = StartProxy(this.settings, saveCredentials: true, checkPreviousBackup: true);
				ClearStarter();
			}

			// notify
			OnProxyStateChanged();

			// log
			LogProxyStarted(resuming);

			return;
		}

		public void GiveUpScheduledStarting() {
			lock (this) {
				// clear the starter
				ClearStarter();
			}

			return;
		}

		public void StopProxy(bool systemSessionEnding, int millisecondsTimeout = 0, bool suspending = false) {
			// stop proxy
			bool completed = false;
			lock (this) {
				// state checks
				if (this.runningProxyState == null) {
					return;
				}
				if (suspending) {
					Debug.Assert(this.starter != null);
					// Note that CreateStarter() returns null if proxying should not be resumed
					this.starter = CreateStarter(resuming: true);
				}

				completed = this.runningProxyState.Stop(systemSessionEnding, millisecondsTimeout);
				DisposableUtil.ClearDisposableObject(ref this.runningProxyState);
			}

			// notify
			OnProxyStateChanged();

			// log
			LogProxyStopped(completed, suspending);

			return;
		}

		public void SuspendProxy(int millisecondsTimeout = 0) {
			StopProxy(systemSessionEnding: false, millisecondsTimeout: millisecondsTimeout, suspending: true);
		}

		public void ResumeProxy() {
			lock (this) {
				if (this.starter != null) {
					this.starter.Schedule();
				}				
			}
		}

		public void ScheduleStartProxy() {
			lock (this) {
				if (this.starter == null) {
					Starter starter = CreateStarter(resuming: false);
					Debug.Assert(starter != null);
					this.starter = starter;
					starter.Schedule();
				}
			}
		}

		#endregion


		#region overrides/overridables - argument processing

		protected override bool HandleOption(string name, string value, CommandSettings settings) {
			// handle option
			bool handled = true;
			if (AreSameOptionNames(name, OptionNames.Start)) {
				if (string.IsNullOrWhiteSpace(value)) {
					settings.GUI.Start = true;
				} else {
					settings.GUI.Start = bool.Parse(value);
				}
			} else {
				handled = base.HandleOption(name, value, settings);
			}

			return handled;
		}

		#endregion


		#region overrides/overridables - execution

		public override void Execute(string commandKind, CommandSettings settings) {
			// save the settings
			this.settings = settings;

			// execute command
			base.Execute(commandKind, settings);

			return;
		}

		#endregion


		#region overrides/overridables - misc

		protected virtual void OnSettingsChanged(CommandSettings newSettings, CommandSettings oldSettings) {
			// argument checks
			Debug.Assert(newSettings != null);
			Debug.Assert(oldSettings != null);

			// LogLevel
			Logger.LogLevel = newSettings.LogLevel;

			// implement SettingsChanged event and fire it, when it becomes necessary 
		}

		protected virtual void OnProxyStateChanged() {
			EventHandler proxyStateChanged = this.ProxyStateChanged;
			if (proxyStateChanged != null) {
				try {
					proxyStateChanged(this, EventArgs.Empty);
				} catch (Exception exception) {
					LogError($"Fail to notify ProxyStateChanged event: {exception.Message}");
					// continue
				}
			}
		}

		#endregion


		#region privates

		// This method must be called in lock(this) scope. 
		private Starter CreateStarter(bool resuming) {
			Starter starter = null;

			// prepare arguments
			GUISettings guiSettings = this.Settings.GUI;
			int tryCount = guiSettings.ResumeTryCount;
			if (tryCount == 0 && resuming) {
				// try at least one time
				tryCount = 1;
			}

			// create a starter if necessary
			if (0 < tryCount) {
				starter = new Starter(this, resuming, tryCount, guiSettings.ResumeDelay, guiSettings.ResumeInterval);
			}

			return starter;
		}

		// This method must be called in lock(this) scope. 
		private void ClearStarter() {
			Starter starter = this.starter;
			this.starter = null;
			if (starter != null) {
				starter.Cancel();
			}

			return;
		}

		#endregion
	}
}
