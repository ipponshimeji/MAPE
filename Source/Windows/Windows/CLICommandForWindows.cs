using System;
using Microsoft.Win32;
using MAPE.Command;
using MAPE.Server;
using MAPE.Utils;


namespace MAPE.Windows {
    public abstract class CLICommandForWindows: CLICommandBase {
		#region types

		public static new class OptionNames {
			#region constants

			public const string ProxyOverride = "ProxyOverride";

			#endregion
		}

		#endregion


		#region creation and disposal

		public CLICommandForWindows(ComponentFactoryForWindows componentFactory): base(componentFactory) {
			return;
		}

		#endregion


		#region overrides/overridables - argument processing

		protected override bool HandleOption(string name, string value, Settings settings) {
			// handle option
			bool handled = true;
			if (AreSameOptionNames(name, OptionNames.ProxyOverride)) {
				settings.GetSystemSettingSwitcherSettings(createIfNotExist: true).SetStringValue(SystemSettingsSwitcherForWindows.SettingNames.ProxyOverride, value);
			} else {
				handled = base.HandleOption(name, value, settings);
			}

			return handled;
		}

		#endregion
	}
}
