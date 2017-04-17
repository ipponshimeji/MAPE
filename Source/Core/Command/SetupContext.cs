using System;
using System.Diagnostics;
using MAPE.Command.Settings;


namespace MAPE.Command {
	public class SetupContext {
		#region data

		protected readonly CommandSettings Settings;

		public bool ProxyDetected { get; private set; } = false;

		public bool NeedActualProxy { get; private set; } = false;

		public string DefaultActualProxyHostName { get; private set; }

		public int? DefaultActualProxyPort { get; private set; }

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
			if (switcher.DetectSystemProxy() != null) {
				this.ProxyDetected = true;
			}
			if (this.ProxyDetected == false && settings.SystemSettingsSwitcher.ActualProxy == null) {
				// The authentication proxy cannot be detected automatically.
				// User must set its information explicitly.
				this.NeedActualProxy = true;
			}

			return;
		}

		#endregion
	}
}
