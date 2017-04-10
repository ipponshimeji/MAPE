using System;
using System.Diagnostics;
using MAPE.Command;
using MAPE.Windows.Settings;


namespace MAPE.Windows {
	public class SetupContext {
		#region data

		public readonly CommandForWindowsSettings Settings;

		public bool NeedActualProxy { get; private set; } = false;

		public bool NeedProxyOverride { get; private set; } = false;

		#endregion


		#region properties

		public bool NeedSetup {
			get {
				return (
					this.NeedActualProxy ||
					this.NeedProxyOverride
				);
			}
		}

		#endregion


		#region creation and disposal

		public SetupContext(CommandBase command, CommandForWindowsSettings settings) {
			// argument checks
			if (command == null) {
				throw new ArgumentNullException(nameof(command));
			}
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}

			// initialize members
			this.Settings = settings;

			SystemSettingsSwitcherForWindows switcher = command.ComponentFactory.CreateSystemSettingsSwitcher(command, settings.SystemSettingsSwitcher) as SystemSettingsSwitcherForWindows;
			Debug.Assert(switcher != null);

			// ActualProxy
			if (switcher.ActualProxy == null) {
				// The authentication proxy cannot be detected automatically.
				// User must set its information explicitly.
				this.NeedActualProxy = true;
			}

			// ProxyOverride
			SystemSettingsForWindows current = switcher.GetCurrentSystemSettings();
			if (
				(current.AutoDetect || string.IsNullOrEmpty(current.AutoConfigURL) == false) &&
				string.IsNullOrEmpty(settings.SystemSettingsSwitcher.FilteredProxyOverride)
			) {
				// User must set ProxyOverride if the proxy information is set automatically.
				this.NeedProxyOverride = true;

				// give default value
				// DefaultProxyOverride may be set in config file
				settings.SystemSettingsSwitcher.ProxyOverride = SystemSettingsSwitcherForWindows.GetDefaultProxyOverride();
			}

			return;
		}

		#endregion
	}
}
