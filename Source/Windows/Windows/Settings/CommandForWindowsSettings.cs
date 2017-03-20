using System;
using System.Diagnostics;
using MAPE.Utils;
using MAPE.Command.Settings;


namespace MAPE.Windows.Settings {
	public class CommandForWindowsSettings: CommandSettings {
		#region properties

		public new SystemSettingsSwitcherForWindowsSettings SystemSettingsSwitcher {
			get {
				return (SystemSettingsSwitcherForWindowsSettings)base.SystemSettingsSwitcher;
			}
		}

		#endregion


		#region creation and disposal

		public CommandForWindowsSettings(IObjectData data): base(data) {
		}

		public CommandForWindowsSettings(): this(NullObjectData) {
		}

		public CommandForWindowsSettings(CommandForWindowsSettings src) : base(src) {
		}

		#endregion


		#region overrides/overridables

		protected override MAPE.Utils.Settings Clone() {
			return new CommandForWindowsSettings(this);
		}

		protected override SystemSettingsSwitcherSettings CreateSystemSettingsSwitcherSettings(IObjectData data) {
			// argument checks
			// data can be null

			return new SystemSettingsSwitcherForWindowsSettings(data);
		}

		#endregion
	}
}
