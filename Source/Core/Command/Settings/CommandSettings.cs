using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using MAPE.Utils;
using MAPE.Server.Settings;


namespace MAPE.Command.Settings {
	public class CommandSettings: MAPE.Utils.Settings {
		#region types

		public static class SettingNames {
			#region constants

			public const string Culture = "Culture";

			public const string LogLevel = "LogLevel";

			public const string Credentials = "Credentials";

			public const string Proxy = "Proxy";

			public const string SystemSettingsSwitcher = "SystemSettingsSwitcher";

			#endregion
		}

		public static class Defaults {
			#region constants

			public const TraceLevel LogLevel = TraceLevel.Error;

			#endregion


			#region methods

			public static bool IsDefaultCulture(CultureInfo value) {
				return value == null || value == Thread.CurrentThread.CurrentUICulture;
			}

			public static bool IsDefaultCredentials(IEnumerable<CredentialSettings> value) {
				return value == null || value.Count() == 0;
			}

			#endregion
		}

		#endregion


		#region data

		public TraceLevel LogLevel { get; set; } = Defaults.LogLevel;

		public CultureInfo Culture { get; set; } = null;

		private CredentialSettings[] credentials = null;

		private SystemSettingsSwitcherSettings systemSettingsSwitcher = null;

		private ProxySettings proxy = null;

		#endregion


		#region properties

		public IEnumerable<CredentialSettings> Credentials {
			get {
				return this.credentials;
			}
			set {
				// set the copy of the value
				this.credentials = (value == null)? null: value.ToArray();
			}
		}

		public SystemSettingsSwitcherSettings SystemSettingsSwitcher {
			get {
				return this.systemSettingsSwitcher;
			}
			set {
				// argument checks
				if (value == null) {
					throw new ArgumentNullException(nameof(value));
				}

				this.systemSettingsSwitcher = value;
			}
		}

		public ProxySettings Proxy {
			get {
				return this.proxy;
			}
			set {
				// argument checks
				if (value == null) {
					throw new ArgumentNullException(nameof(value));
				}

				this.proxy = value;
			}
		}

		#endregion


		#region creation and disposal

		public CommandSettings(IObjectData data): base(data) {
			// prepare settings
			TraceLevel logLevel = Defaults.LogLevel;
			CultureInfo culture = null;
			CredentialSettings[] credentials = null;
			SystemSettingsSwitcherSettings systemSettingsSwitcher = null;
			ProxySettings proxy = null;
			if (data != null) {
				// get settings from data
				logLevel = (TraceLevel)data.GetEnumValue(SettingNames.LogLevel, logLevel, typeof(TraceLevel));
				culture = data.GetValue(SettingNames.Culture, culture, ExtractCultureInfoValue);
				credentials = data.GetObjectArrayValue(SettingNames.Credentials, credentials, this.CreateCredentialSettings);
				systemSettingsSwitcher = data.GetObjectValue(SettingNames.SystemSettingsSwitcher, systemSettingsSwitcher, this.CreateSystemSettingsSwitcherSettings);
				proxy = data.GetObjectValue(SettingNames.Proxy, proxy, this.CreateProxySettings);
			}
			if (systemSettingsSwitcher == null) {
				// SystemSettingsSwitcher cannot be null
				systemSettingsSwitcher = CreateSystemSettingsSwitcherSettings(null);
			}
			if (proxy == null) {
				// Proxy cannot be null
				proxy = CreateProxySettings(null);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.LogLevel = logLevel;
				this.Culture = culture;
				this.Credentials = credentials;
				this.SystemSettingsSwitcher = systemSettingsSwitcher;
				this.Proxy = proxy;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public CommandSettings(): this(null) {
		}

		#endregion


		#region overrides/overridables

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// save settings
			data.SetEnumValue(SettingNames.LogLevel, this.LogLevel, omitDefault, this.LogLevel == Defaults.LogLevel);
			data.SetValue(SettingNames.Culture, this.Culture, CreateCultureInfoValue, omitDefault, Defaults.IsDefaultCulture(this.Culture));
			data.SetObjectArrayValue(SettingNames.Credentials, this.Credentials, omitDefault, Defaults.IsDefaultCredentials(this.Credentials));
			data.SetObjectValue(SettingNames.SystemSettingsSwitcher, this.SystemSettingsSwitcher, true);	// overwrite existing settings, not omittable
			data.SetObjectValue(SettingNames.Proxy, this.Proxy);											// not omittable

			return;
		}

		protected virtual CredentialSettings CreateCredentialSettings(IObjectData data) {
			// argument checks
			// data can be null

			return new CredentialSettings(data);
		}

		protected virtual SystemSettingsSwitcherSettings CreateSystemSettingsSwitcherSettings(IObjectData data) {
			// argument checks
			// data can be null

			return new SystemSettingsSwitcherSettings(data);
		}

		protected virtual ProxySettings CreateProxySettings(IObjectData data) {
			// argument checks
			// data can be null

			return new ProxySettings(data);
		}

		#endregion


		#region private

		private static CultureInfo ExtractCultureInfoValue(IObjectDataValue value) {
			// argument checks
			Debug.Assert(value != null);

			// extract CultureInfo value
			string cultureName = value.ExtractStringValue();
			return (cultureName == null) ? null : new CultureInfo(cultureName);
		}

		private static IObjectDataValue CreateCultureInfoValue(IObjectData data, CultureInfo value) {
			// argument checks
			Debug.Assert(data != null);

			// create CultureInfo value
			return data.CreateValue((value == null) ? null : value.Name);
		}

		#endregion
	}
}
