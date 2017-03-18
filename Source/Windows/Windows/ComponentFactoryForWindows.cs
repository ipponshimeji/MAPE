using System;
using MAPE;
using MAPE.Utils;
using MAPE.Command;
using MAPE.Command.Settings;
using MAPE.Server;


namespace MAPE.Windows {
    public class ComponentFactoryForWindows: ComponentFactory {
		#region methods

		public override CommandSettings CreateCommandSettings(IObjectData data) {
			return new Settings.CommandForWindowsSettings(data);
		}

		public override SystemSettingsSwitcher CreateSystemSettingsSwitcher(CommandBase owner, SettingsData settings, Proxy proxy) {
			return new SystemSettingsSwitcherForWindows(owner, settings, proxy);
		}

		#endregion
	}
}
