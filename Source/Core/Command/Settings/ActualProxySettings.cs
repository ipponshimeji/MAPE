using System;
using System.Diagnostics;
using System.Net;
using MAPE.Utils;


namespace MAPE.Command.Settings {
	public class ActualProxySettings: MAPE.Utils.Settings {
		#region types

		public static class SettingNames {
			#region constants

			public const string Host = "Host";

			public const string Port = "Port";

			public const string ConfigurationScript = "ConfigurationScript";

			#endregion
		}

		public static class Defaults {
			#region constants

			public const string Host = "proxy.example.org";

			public const int Port = 8080;

			public const string ConfigurationScript = null;

			#endregion


			#region methods

			public static bool IsDefaultHostName(string hostName) {
				return AreSameHostName(Host, hostName);
			}

			#endregion
		}

		#endregion


		#region data

		private string host;

		private int port;

		private string configurationScript;

		#endregion


		#region properties

		public string Host {
			get {
				return this.host;
			}
			set {
				// argument checks
				if (string.IsNullOrEmpty(value)) {
					if (this.configurationScript == null) {
						// the value should not null or empty if configuration script is not specified
						throw CreateArgumentNullOrEmptyException(nameof(value), SettingNames.Host);
					} else {
						value = null;
					}
				}

				this.host = value;
			}
		}

		public int Port {
			get {
				return this.port;
			}
			set {
				// argument checks
				if (value < IPEndPoint.MinPort || IPEndPoint.MaxPort < value) {
					throw new ArgumentOutOfRangeException(nameof(value), $"The '{SettingNames.Port}' value must be between {IPEndPoint.MinPort} and {IPEndPoint.MaxPort}, inclusive.");
				}

				this.port = value;
			}
		}

		public string ConfigurationScript {
			get {
				return this.configurationScript;
			}
			set {
				// argument checks
				if (string.IsNullOrEmpty(value)) {
					if (this.host == null) {
						// the value should not null or empty if host is not specified
						throw CreateArgumentNullOrEmptyException(nameof(value), SettingNames.ConfigurationScript);
					} else {
						value = null;
					}
				}

				this.configurationScript = value;
			}
		}

		#endregion


		#region creation and disposal

		public ActualProxySettings(IObjectData data): base(data) {
			// prepare settings
			string host = Defaults.Host;
			int port = Defaults.Port;
			string configurationScript = Defaults.ConfigurationScript;
			if (data != null) {
				// get settings from data
				configurationScript = data.GetStringValue(SettingNames.ConfigurationScript, configurationScript);
				host = data.GetStringValue(SettingNames.Host, string.IsNullOrEmpty(configurationScript)? host: null);
				port = data.GetInt32Value(SettingNames.Port, port);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value

				// Note that non-empty value should be set first,
				// because an ArgumentNullException will be thrown 
				// if both this.Host and this.this.ConfigurationScript are null.
				if (string.IsNullOrEmpty(configurationScript)) {
					this.Host = host;
					this.ConfigurationScript = configurationScript;
				} else {
					this.ConfigurationScript = configurationScript;
					this.Host = host;
				}
				this.Port = port;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public ActualProxySettings(): this(NullObjectData) {
		}

		public ActualProxySettings(ActualProxySettings src): base(src) {
			// argument checks
			if (src == null) {
				throw new ArgumentNullException(nameof(src));
			}

			// clone members
			this.host = src.host;
			this.port = src.port;
			this.configurationScript = src.configurationScript;

			return;
		}
	
		#endregion


		#region methods

		public static bool AreSameHostName(string hostName1, string hostName2) {
			return string.Compare(hostName1, hostName2, StringComparison.OrdinalIgnoreCase) == 0;
		}

		public WebProxy CreateWebProxy() {
			// state checks
			EnsureIsValid();
			Debug.Assert(string.IsNullOrEmpty(this.Host) == false);
			Debug.Assert(IPEndPoint.MinPort <= this.Port && this.Port <= IPEndPoint.MaxPort);

			// create a WebProxy object
			return new WebProxy(this.Host, this.Port);
		}

		#endregion


		#region overrides

		protected override MAPE.Utils.Settings Clone() {
			return new ActualProxySettings(this);
		}

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// state checks
			EnsureIsValid();
			Debug.Assert(string.IsNullOrEmpty(this.Host) == false || string.IsNullOrEmpty(this.ConfigurationScript) == false);
			Debug.Assert(IPEndPoint.MinPort <= this.Port && this.Port <= IPEndPoint.MaxPort);

			// save settings (these settings are not omittable)
			string host = this.Host;
			bool isDefault = (host == null || AreSameHostName(host, Defaults.Host));
			data.SetStringValue(SettingNames.Host, this.Host, omitDefault, isDefault);
			data.SetInt32Value(SettingNames.Port, this.Port, omitDefault, this.Port == Defaults.Port);
			data.SetStringValue(SettingNames.ConfigurationScript, this.ConfigurationScript, omitDefault, this.ConfigurationScript == Defaults.ConfigurationScript);

			return;
		}

		#endregion


		#region privates

		private void EnsureIsValid() {
			// state checks
			if (string.IsNullOrEmpty(this.Host) && string.IsNullOrEmpty(this.ConfigurationScript)) {
				throw new FormatException($"Either '{nameof(this.Host)}' or '{nameof(this.ConfigurationScript)}' must be specified.");
			}

			return;
		}

		#endregion
	}
}
