using System;
using System.Collections.Generic;
using System.Diagnostics;
using MAPE.Utils;
using MAPE.Command.Settings;


namespace MAPE.Command {
    public abstract class GUICommandBase: CommandBase {
		#region types

		public static new class OptionNames {
			#region constants

			public const string Start = "Start";

			#endregion
		}

		#endregion


		#region data

		private CommandSettings settings;

		private RunningProxyState runningProxyState = null;

		private bool suspending = false;

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
			if (this.runningProxyState != null) {
				StopProxy();
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
				if (resuming) {
					this.suspending = false;
				}
				if (this.runningProxyState != null) {
					return;
				}

				// start 
				this.runningProxyState = StartProxy(this.settings, this);
			}

			// notify
			OnProxyStateChanged();

			// log
			LogProxyStarted(resuming);

			return;
		}

		public void StopProxy(int millisecondsTimeout = 0, bool suspending = false) {
			// stop proxy
			bool completed = false;
			lock (this) {
				// state checks
				if (this.runningProxyState == null) {
					return;
				}
				if (suspending) {
					this.suspending = true;
				}

				completed = this.runningProxyState.Stop(millisecondsTimeout);
				Util.DisposeWithoutFail(ref this.runningProxyState);
			}

			// notify
			OnProxyStateChanged();

			// log
			LogProxyStopped(completed, suspending);

			return;
		}

		public void SuspendProxy(int millisecondsTimeout = 0) {
			StopProxy(millisecondsTimeout, suspending: true);
		}

		public void ResumeProxy() {
			StartProxy(resuming: true);
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
	}
}
