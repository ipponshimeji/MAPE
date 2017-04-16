using System;
using System.Diagnostics;
using MAPE.Command;
using MAPE.Windows.Settings;


namespace MAPE.Windows {
	public class SetupContextForWindows: SetupContext {
		#region data

		public bool NeedProxyOverride { get; private set; } = false;

		public string DefaultProxyOverride { get; private set; }

		#endregion


		#region properties

		public new CommandForWindowsSettings Settings {
			get {
				return (CommandForWindowsSettings)base.Settings;
			}
		}

		public override bool NeedSetup {
			get {
				return this.NeedProxyOverride || base.NeedSetup;
			}
		}

		#endregion


		#region creation and disposal

		public SetupContextForWindows(CommandForWindowsSettings settings, SystemSettingsSwitcherForWindows switcher): base(settings, switcher) {
			// argument checks
			Debug.Assert(settings != null);
			Debug.Assert(switcher != null);

			// ProxyOverride
			this.DefaultProxyOverride = SystemSettingsSwitcherForWindows.GetDefaultProxyOverride();
			SystemSettingsForWindows current = switcher.GetCurrentSystemSettings();
			if (
				(current.AutoDetect || string.IsNullOrEmpty(current.AutoConfigURL) == false) &&
				string.IsNullOrEmpty(settings.SystemSettingsSwitcher.ProxyOverride)
			) {
				// User must set ProxyOverride if the proxy information is set automatically.
				this.NeedProxyOverride = true;

				// give default value
				// DefaultProxyOverride may be set in config file
				settings.SystemSettingsSwitcher.ProxyOverride = this.DefaultProxyOverride;
			}

			return;
		}

		public SetupContextForWindows(CommandForWindowsSettings settings, CommandBase command): this(settings, GetSystemSettingsSwitcher(settings, command)) {
		}

		#endregion


		#region privates

		private static SystemSettingsSwitcherForWindows GetSystemSettingsSwitcher(CommandForWindowsSettings settings, CommandBase command) {
			// argument checks
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}
			if (command == null) {
				throw new ArgumentNullException(nameof(command));
			}

			return (SystemSettingsSwitcherForWindows)command.ComponentFactory.CreateSystemSettingsSwitcher(command, null);
		}

		#endregion
	}
}
