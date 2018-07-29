using System;
using System.Diagnostics;
using MAPE.Utils;
using MAPE.Server;
using MAPE.Command.Settings;


namespace MAPE.Command {
	public class SetupContext {
		#region constants

		// SetupLevels
		// 0:          - 0.1.10.0
		// 1: 1.0.11.0 - 1.0.16.0
		// 2: 1.0.17.0 -
		public const int LatestInitialSetupLevel = 2;

		#endregion


		#region data

		protected readonly CommandSettings Settings;

		public bool ProxyDetected { get; private set; } = false;

		public bool NeedActualProxy { get; private set; } = false;

		public string DefaultActualProxyHostName { get; private set; }

		public int? DefaultActualProxyPort { get; private set; }

		public string DefaultActualProxyConfigurationScript { get; private set; }

		#endregion


		#region properties

		public virtual bool NeedSetup {
			get {
				return this.NeedActualProxy;
			}
		}

		public bool IsDefaultActualProxyProvided {
			get {
				return string.IsNullOrEmpty(this.DefaultActualProxyHostName) == false && this.DefaultActualProxyPort != null;
			}
		}

		#endregion


		#region creation and disposal

		public SetupContext(CommandSettings settings, SystemSettingsSwitcher switcher) {
			// argument checks
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}
			if (switcher == null) {
				throw new ArgumentNullException(nameof(switcher));
			}

			// initialize members
			this.Settings = settings;

			// ActualProxy
			this.DefaultActualProxyHostName = SystemSettingsSwitcher.GetDefaultActualProxyHostName();
			this.DefaultActualProxyPort = SystemSettingsSwitcher.GetDefaultActualProxyPort();
			this.DefaultActualProxyConfigurationScript = SystemSettingsSwitcher.GetDefaultActualProxyConfigurationScript();
			IActualProxy actualProxy = switcher.DetectSystemActualProxy();
			if (actualProxy != null) {
				this.ProxyDetected = true;
				DisposableUtil.ClearDisposableObject(ref actualProxy);
			}
			if (this.ProxyDetected == false && settings.SystemSettingsSwitcher.ActualProxy == null) {
				// The authentication proxy cannot be detected automatically.
				// User must set its information explicitly.
				this.NeedActualProxy = true;
			}

			return;
		}

		#endregion


		#region overridables

		public virtual ActualProxySettings CreateActualProxySettings() {
			// create an ActualProxySettings instance
			ActualProxySettings actualProxySettings = new ActualProxySettings();

			// set up the instance
			if (string.IsNullOrEmpty(this.DefaultActualProxyHostName) == false) {
				actualProxySettings.Host = this.DefaultActualProxyHostName;
			}
			if (this.DefaultActualProxyPort != null) {
				actualProxySettings.Port = this.DefaultActualProxyPort.Value;
			}
			if (string.IsNullOrEmpty(this.DefaultActualProxyConfigurationScript) == false) {
				actualProxySettings.ConfigurationScript = this.DefaultActualProxyConfigurationScript;
			}

			return actualProxySettings;
		}

		#endregion
	}
}
