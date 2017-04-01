using System;
using MAPE;
using MAPE.Utils;
using MAPE.Command;
using MAPE.Command.Settings;
using MAPE.Server;
using MAPE.Windows.Settings;


namespace MAPE.Windows {
    public class ComponentFactoryForWindows: ComponentFactory {
		#region methods

		public override CommandSettings CreateCommandSettings(IObjectData data) {
			return new Settings.CommandForWindowsSettings(data);
		}

		public override SystemSettingsSwitcher CreateSystemSettingsSwitcher(CommandBase owner, SystemSettingsSwitcherSettings settings) {
			// argument checks
			SystemSettingsSwitcherForWindowsSettings actualSettings = null;
			if (settings != null) {
				actualSettings = settings as SystemSettingsSwitcherForWindowsSettings;
				if (actualSettings == null) {
					throw new ArgumentNullException($"It must be {nameof(SystemSettingsSwitcherForWindowsSettings)} class.", nameof(settings));
				}
			}

			return new SystemSettingsSwitcherForWindows(owner, actualSettings);
		}

		#endregion
	}
}
