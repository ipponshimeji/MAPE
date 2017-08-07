using System;
using System.Diagnostics;
using MAPE.Command;
using MAPE.Windows.Settings;


namespace MAPE.Windows {
	public class SetupContextForWindows: SetupContext {
		#region data

		public bool NeedProxyOverride { get; private set; } = false;

		public string DefaultProxyOverride { get; private set; }

		public bool ShouldResetProxyOverride { get; private set; } = false;

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
			SystemSettingsSwitcherForWindowsSettings switcherSettings = settings.SystemSettingsSwitcher;
			Debug.Assert(switcherSettings != null);
			Debug.Assert(switcher != null);

			// ProxyOverride
			this.DefaultProxyOverride = SystemSettingsSwitcherForWindows.GetDefaultProxyOverride();
			if (settings.InitialSetupLevel == 0 || base.NeedActualProxy) {
				// User must set ProxyOverride
				this.NeedProxyOverride = true;

				if (string.IsNullOrEmpty(switcherSettings.ProxyOverride)) {
					// give default value
					// DefaultProxyOverride may be set in config file
					switcherSettings.ProxyOverride = this.DefaultProxyOverride;
				}
			}

			if (settings.InitialSetupLevel == 1) {
				// After MAPE supports auto detect and auto config script,
				// ProxyOverride should be reviewed because only overrides which
				// are not handled by auto config should be specified.
				SystemSettingsForWindows current = switcher.GetCurrentSystemSettings();
				if (
					(current.AutoDetect || string.IsNullOrEmpty(current.AutoConfigURL) == false) &&
					string.CompareOrdinal(switcherSettings.ProxyOverride, this.DefaultProxyOverride) != 0
				) {
					this.NeedProxyOverride = true;
					this.ShouldResetProxyOverride = true;
				}
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
