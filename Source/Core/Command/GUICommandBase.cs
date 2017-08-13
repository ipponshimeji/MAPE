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

		protected class Resumer {
			#region data

			public readonly GUICommandBase Owner;

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

			public Resumer(GUICommandBase owner, int tryCount, int delay, int interval) {
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
				this.TryCount = tryCount;
				this.Delaly = delay;
				this.Interval = interval;

				return;
			}

			#endregion


			#region methods

			public void StartResuming() {
				Task task;
				lock (this.instanceLocker) {
					// state checks
					if (this.canceled) {
						return;
					}
					if (this.task != null) {
						// already resuming
						return;
					}

					// create resuming task
					task = new Task(this.Resume);
					this.task = task;
				}

				// start the resuming task
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

			private void Resume() {
				GUICommandBase owner = this.Owner;
				int counter = this.TryCount;
				string logMessage;

				// resume proxying 
				logMessage = string.Format(Resources.GUICommandBase_Message_Resuming, this.Delaly);
				owner.LogVerbose(logMessage);
				Thread.Sleep(this.Delaly);
				Debug.Assert(0 < counter);
				do {
					// check whether resuming was canceled
					lock (this.instanceLocker) {
						if (this.canceled) {
							break;
						}
					}

					// try to resume proxying
					try {
						// Note the proxy won't start actually if this.suspended is false
						owner.StartProxy(resuming: true);
						break;
					} catch {
						// continue;				
					}

					// prepare the next try
					--counter;
					if (counter <= 0) {
						// fail to resume
						owner.GiveUpResuming();
						owner.LogError(Resources.GUICommandBase_Message_FailToResume);
						break;
					}
					logMessage = string.Format(Resources.GUICommandBase_Message_RetryResuming, this.Interval);
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

		private Resumer resumer = null;

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
			GiveUpResuming();
			if (this.runningProxyState != null) {
				StopProxy(systemSessionEnding: false);
			}

			// dispose the base class level
			base.Dispose();
		}

		#endregion


		#region methods

		public void StartProxy(bool resuming = false) {
			// start proxy
			lock (this) {
				// state checks
				Resumer resumer = this.resumer;
				if (resuming && resumer == null) {
					// resume only when the proxying was suspended
					return;
				}
				if (this.runningProxyState != null) {
					ClearResumer();	// no use if proxying is running
					return;
				}

				// start
				// Note that the resumer won't be cleared if StartProxy() throws an exception. 
				this.runningProxyState = StartProxy(this.settings, saveCredentials: true, checkPreviousBackup: true);
				ClearResumer();
			}

			// notify
			OnProxyStateChanged();

			// log
			LogProxyStarted(resuming);

			return;
		}

		public void GiveUpResuming() {
			// start proxy
			lock (this) {
				// clear the resumer
				ClearResumer();
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
					Debug.Assert(this.resumer != null);
					// Note that CreateResumer() returns null if proxying should not be resumed
					this.resumer = CreateResumer();
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
				if (this.resumer != null) {
					this.resumer.StartResuming();
				}				
			}
		}

		#endregion


		#region overrides/overridables - argument processing

		protected override bool HandleOption(string name, string value, CommandSettings settings) {
			// handle option
			bool handled = true;
			if (AreSameOptionNames(name, OptionNames.Start)) {
				settings.GUI.Start = bool.Parse(value);
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
		private Resumer CreateResumer() {
			Resumer resumer = null;

			GUISettings guiSettings = this.Settings.GUI;
			if (0 < guiSettings.ResumeTryCount) {
				resumer = new Resumer(this, guiSettings.ResumeTryCount, guiSettings.ResumeDelay, guiSettings.ResumeInterval);
			}

			return resumer;
		}

		// This method must be called in lock(this) scope. 
		private void ClearResumer() {
			Resumer resumer = this.resumer;
			this.resumer = null;
			if (resumer != null) {
				resumer.Cancel();
			}

			return;
		}

		#endregion
	}
}
