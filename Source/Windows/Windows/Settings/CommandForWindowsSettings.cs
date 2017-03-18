using System;
using System.Diagnostics;
using MAPE.Utils;
using MAPE.Command.Settings;


namespace MAPE.Windows.Settings {
	public class CommandForWindowsSettings: CommandSettings {
		#region creation and disposal

		public CommandForWindowsSettings(IObjectData data): base(data) {
		}

		public CommandForWindowsSettings(): this(null) {
		}

		#endregion


		#region overrides/overridables

		protected override SystemSettingsSwitcherSettings CreateSystemSettingsSwitcherSettings(IObjectData data) {
			// argument checks
			// data can be null

			return new SystemSettingsSwitcherForWindowsSettings(data);
		}

		#endregion
	}
}
