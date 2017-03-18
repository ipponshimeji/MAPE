using System;
using System.Diagnostics;
using MAPE.Utils;
using MAPE.Command.Settings;


namespace MAPE.Windows.Settings {
	public class SystemSettingsSwitcherForWindowsSettings: SystemSettingsSwitcherSettings {
		#region types

		public static new class SettingNames {
			#region constants

			public const string ProxyOverride = "ProxyOverride";

			#endregion
		}

		public static new class Defaults {
			#region constants

			public const string ProxyOverride = "";

			#endregion
		}

		#endregion


		#region data

		private string proxyOverride;

		#endregion


		#region properties

		public string ProxyOverride {
			get {
				return this.proxyOverride;
			}
			set {
				// argument checks
				if (value == null) {
					value = string.Empty;
				}

				this.proxyOverride = value;
			}
		}

		#endregion


		#region creation and disposal

		public SystemSettingsSwitcherForWindowsSettings(IObjectData data): base(data) {
			// prepare settings
			string proxyOverride = Defaults.ProxyOverride;
			if (data != null) {
				// get settings from data
				proxyOverride = data.GetStringValue(SettingNames.ProxyOverride, proxyOverride);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.ProxyOverride = proxyOverride;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public SystemSettingsSwitcherForWindowsSettings() : this(null) {
		}

		#endregion


		#region overridables

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// save the base class level settings
			base.SaveTo(data, omitDefault);

			// save this class level settings
			data.SetStringValue(SettingNames.ProxyOverride, this.ProxyOverride, omitDefault, this.ProxyOverride == Defaults.ProxyOverride);

			return;
		}

		#endregion
	}
}
