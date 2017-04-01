using System;
using System.Diagnostics;
using System.Net;
using MAPE.Utils;
using MAPE.Command;


namespace MAPE.Windows {
	public class SystemSettingsForWindows: SystemSettings {
		#region types

		public static class SettingNames {
			#region constants

			public const string AutoConfigURL = "AutoConfigURL";

			public const string ProxyEnable = "ProxyEnable";

			public const string ProxyServer = "ProxyServer";

			public const string ProxyOverride = "ProxyOverride";

			public const string AutoDetect = "AutoDetect";

			public const string HttpProxyEnvironmentVariable = "HttpProxyEnvironmentVariable";

			public const string HttpsProxyEnvironmentVariable = "HttpsProxyEnvironmentVariable";

			#endregion
		}

		public static class Defaults {
			#region constants

			public const string AutoConfigURL = null;

			#endregion
		}

		#endregion


		#region data

		public string AutoConfigURL { get; set; } = null;

		public int? ProxyEnable { get; set; } = null;

		// ex. http=proxy.example.org:8080;https=proxy.example.org:8080
		public string ProxyServer { get; set; } = null;

		// ex. *.example.org;*.example.jp;<local>
		public string ProxyOverride { get; set; } = null;

		public bool AutoDetect { get; set; } = false;

		public string HttpProxyEnvironmentVariable { get; set; } = null;

		public string HttpsProxyEnvironmentVariable { get; set; } = null;

		#endregion


		#region creation and disposal

		public SystemSettingsForWindows(IObjectData data): base(data) {
			// prepare settings
			string autoConfigURL = null;
			int? proxyEnable = null;
			string proxyServer = null;
			string proxyOverride = null;
			bool autoDetect = false;
			string httpProxyEnvironmentVariable = null;
			string httpsProxyEnvironmentVariable = null;

			if (data != null) {
				// get settings from data
				autoConfigURL = data.GetStringValue(SettingNames.AutoConfigURL, null);
				proxyEnable = data.GetValue(SettingNames.ProxyEnable, ObjectDataExtension.ExtractInt32Value);
				proxyServer = data.GetStringValue(SettingNames.ProxyServer, null);
				proxyOverride = data.GetStringValue(SettingNames.ProxyOverride, null);
				autoDetect = data.GetBooleanValue(SettingNames.AutoDetect, false);
				httpProxyEnvironmentVariable = data.GetStringValue(SettingNames.HttpProxyEnvironmentVariable, null);
				httpsProxyEnvironmentVariable = data.GetStringValue(SettingNames.HttpsProxyEnvironmentVariable, null);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.AutoConfigURL = autoConfigURL;
				this.ProxyEnable = proxyEnable;
				this.ProxyServer = proxyServer;
				this.ProxyOverride = proxyOverride;
				this.AutoDetect = autoDetect;
				this.HttpProxyEnvironmentVariable = httpProxyEnvironmentVariable;
				this.HttpsProxyEnvironmentVariable = httpsProxyEnvironmentVariable;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public SystemSettingsForWindows(): this(NullObjectData) {
		}

		public SystemSettingsForWindows(SystemSettingsForWindows src) : base(src) {
			// argument checks
			if (src == null) {
				throw new ArgumentNullException(nameof(src));
			}

			// clone members
			this.AutoConfigURL = src.AutoConfigURL;
			this.ProxyEnable = src.ProxyEnable;
			this.ProxyServer = src.ProxyServer;
			this.ProxyOverride = src.ProxyOverride;
			this.AutoDetect = src.AutoDetect;
			this.HttpProxyEnvironmentVariable = src.HttpProxyEnvironmentVariable;
			this.HttpsProxyEnvironmentVariable = src.HttpsProxyEnvironmentVariable;

			return;
		}

		#endregion


		#region overrides/overridables

		protected override MAPE.Utils.Settings Clone() {
			return new SystemSettingsForWindows(this);
		}

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// save settings
			data.SetStringValue(SettingNames.AutoConfigURL, this.AutoConfigURL);
			data.SetValue(SettingNames.ProxyEnable, this.ProxyEnable, ObjectDataExtension.CreateInt32Value);
			data.SetStringValue(SettingNames.ProxyServer, this.ProxyServer);
			data.SetStringValue(SettingNames.ProxyOverride, this.ProxyOverride);
			data.SetBooleanValue(SettingNames.AutoDetect, this.AutoDetect);
			data.SetStringValue(SettingNames.HttpProxyEnvironmentVariable, this.HttpProxyEnvironmentVariable);
			data.SetStringValue(SettingNames.HttpsProxyEnvironmentVariable, this.HttpsProxyEnvironmentVariable);

			return;
		}

		#endregion
	}
}
