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

		#endregion


		#region methods

		public void StartProxy() {
			lock (this) {
				// state checks
				if (this.runningProxyState != null) {
					throw new InvalidOperationException("Already started.");
				}

				this.runningProxyState = StartProxy(this.settings, this);
			}

			return;
		}

		public void StopProxy() {
			lock (this) {
				// state checks
				if (this.runningProxyState == null) {
					return;
				}

				Util.DisposeWithoutFail(ref this.runningProxyState);
			}

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
	}
}
