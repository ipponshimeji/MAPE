using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using MAPE.Utils;
using MAPE.Server;


namespace MAPE.Command {
    public abstract class GUICommandBase: CommandBase {
		#region types

		public static new class OptionNames {
			#region constants

			public const string Start = "Start";

			#endregion
		}

		public static new class SettingNames {
			#region constants

			public const string Start = "Start";

			#endregion
		}

		#endregion


		#region data

		private Settings settings;

		private RunningProxyState runningProxyState = null;

		protected Queue<string> ErrorMessages { get; private set; } = new Queue<string>();

		#endregion


		#region properties

		public bool IsProxyRunning {
			get {
				return this.runningProxyState != null;
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
			this.ErrorMessages = null;

			// dispose the base class level
			base.Dispose();
		}

		#endregion


		#region methods

		public void StartProxy() {
			// start proxy
			lock (this) {
				// state checks
				if (this.runningProxyState != null) {
					return;
				}

				this.runningProxyState = StartProxy(this.settings, this);
			}

			// log
			LogStart("Proxy Started.");

			return;
		}

		public void StopProxy(int millisecondsTimeout = 0) {
			// stop proxy
			lock (this) {
				// state checks
				if (this.runningProxyState == null) {
					return;
				}

				this.runningProxyState.Stop(millisecondsTimeout);
				Util.DisposeWithoutFail(ref this.runningProxyState);
			}

			// log
			LogStop("Proxy Stopped.");

			return;
		}

		#endregion


		#region overrides/overridables - argument processing

		protected override bool HandleOption(string name, string value, Settings settings) {
			// handle option
			bool handled = true;
			if (AreSameOptionNames(name, OptionNames.Start)) {
				settings.SetJsonValue(SettingNames.Start, value);
			} else {
				handled = base.HandleOption(name, value, settings);
			}

			return handled;
		}

		#endregion


		#region overrides/overridables - execution

		public override void Execute(string commandKind, Settings settings) {
			// save the settings
			this.settings = settings;

			base.Execute(commandKind, settings);
		}

		#endregion


		#region overrides/overridables - misc

		protected override void ShowErrorMessage(string message) {
			// state checks
			if (this.ErrorMessages == null) {
				throw CreateObjectDisposedException();
			}

			// queue the error message
			// Queued messages are processed by derived classes
			// when GUI is available.
			this.ErrorMessages.Enqueue(message);
		}

		#endregion
	}
}
