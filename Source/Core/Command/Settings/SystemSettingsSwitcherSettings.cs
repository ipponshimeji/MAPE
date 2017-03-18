using System;
using System.Diagnostics;
using MAPE.Utils;


namespace MAPE.Command.Settings {
	public class SystemSettingsSwitcherSettings: MAPE.Utils.Settings {
		#region types

		public static class SettingNames {
			#region constants

			public const string EnableSystemSettingsSwitch = "EnableSystemSettingsSwitch";

			public const string ActualProxy = "ActualProxy";

			#endregion
		}

		public static class Defaults {
			#region constants

			public const bool EnableSystemSettingsSwitch = true;

			#endregion
		}

		#endregion


		#region data

		public bool EnableSystemSettingsSwitch { get; set; }

		public ActualProxySettings ActualProxy { get; set; }

		#endregion


		#region creation and disposal

		public SystemSettingsSwitcherSettings(IObjectData data): base(data) {
			// prepare settings
			bool enableSystemSettingsSwitch = Defaults.EnableSystemSettingsSwitch;
			ActualProxySettings actualProxy = null;
			if (data != null) {
				// get settings from data
				enableSystemSettingsSwitch = data.GetBooleanValue(SettingNames.EnableSystemSettingsSwitch, enableSystemSettingsSwitch);
				// Note that ActualProxy should not be empty but null if the settings do not exist.
				actualProxy = data.GetObjectValue(SettingNames.ActualProxy, actualProxy, this.CreateActualProxySettings);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.EnableSystemSettingsSwitch = enableSystemSettingsSwitch;
				this.ActualProxy = actualProxy;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public SystemSettingsSwitcherSettings(): this(null) {
		}

		#endregion


		#region overrides/overridables

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// save settings
			data.SetBooleanValue(SettingNames.EnableSystemSettingsSwitch, this.EnableSystemSettingsSwitch, omitDefault, this.EnableSystemSettingsSwitch == Defaults.EnableSystemSettingsSwitch);
			data.SetObjectValue(SettingNames.ActualProxy, this.ActualProxy, true, omitDefault, this.ActualProxy == null);

			return;
		}

		protected virtual ActualProxySettings CreateActualProxySettings(IObjectData data) {
			// argument checks
			// data can be null

			return new ActualProxySettings(data);
		}

		#endregion
	}
}
