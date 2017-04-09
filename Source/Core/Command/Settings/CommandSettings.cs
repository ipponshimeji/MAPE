using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MAPE.Utils;
using MAPE.Server.Settings;


namespace MAPE.Command.Settings {
	public class CommandSettings: MAPE.Utils.Settings {
		#region types

		public static class SettingNames {
			#region constants

			public const string InitialSetupDone = "InitialSetupDone";

			public const string Culture = "Culture";

			public const string LogLevel = "LogLevel";

			public const string NoLogo = "NoLogo";

			public const string Credentials = "Credentials";

			public const string Proxy = "Proxy";

			public const string SystemSettingsSwitcher = "SystemSettingsSwitcher";

			public const string GUI = "GUI";

			#endregion
		}

		public static class Defaults {
			#region constants

			public const bool InitialSetupDone = false;

			public const TraceLevel LogLevel = TraceLevel.Error;

			public const bool NoLogo = false;

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

		public bool InitialSetupDone { get; set; }

		public TraceLevel LogLevel { get; set; }

		public CultureInfo Culture { get; set; }

		public bool NoLogo { get; set; }

		private IEnumerable<CredentialSettings> credentials;

		private SystemSettingsSwitcherSettings systemSettingsSwitcher;

		private ProxySettings proxy;

		private GUISettings gui;

		#endregion


		#region properties

		public IEnumerable<CredentialSettings> Credentials {
			get {
				return this.credentials;
			}
			set {
				this.credentials = value;
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

		public GUISettings GUI {
			get {
				return this.gui;
			}
			set {
				// argument checks
				if (value == null) {
					throw new ArgumentNullException(nameof(value));
				}

				this.gui = value;
			}
		}

		#endregion


		#region creation and disposal

		public CommandSettings(IObjectData data): base(data) {
			// prepare settings
			bool initialSetupDone = Defaults.InitialSetupDone;
			TraceLevel logLevel = Defaults.LogLevel;
			CultureInfo culture = null;
			bool noLogo = Defaults.NoLogo;
			CredentialSettings[] credentials = null;
			SystemSettingsSwitcherSettings systemSettingsSwitcher = null;
			GUISettings gui = null;
			ProxySettings proxy = null;
			if (data != null) {
				// get settings from data
				initialSetupDone = data.GetBooleanValue(SettingNames.InitialSetupDone, Defaults.InitialSetupDone);
				logLevel = (TraceLevel)data.GetEnumValue(SettingNames.LogLevel, logLevel, typeof(TraceLevel));
				culture = data.GetValue(SettingNames.Culture, culture, ExtractCultureInfoValue);
				noLogo = data.GetBooleanValue(SettingNames.NoLogo, noLogo);
				credentials = data.GetObjectArrayValue(SettingNames.Credentials, credentials, CreateCredentialSettings);
				systemSettingsSwitcher = data.GetObjectValue(SettingNames.SystemSettingsSwitcher, systemSettingsSwitcher, this.CreateSystemSettingsSwitcherSettings);
				proxy = data.GetObjectValue(SettingNames.Proxy, proxy, this.CreateProxySettings);
				gui = data.GetObjectValue(SettingNames.GUI, gui, this.CreateGUISettings);
			}
			if (systemSettingsSwitcher == null) {
				// SystemSettingsSwitcher cannot be null
				systemSettingsSwitcher = CreateSystemSettingsSwitcherSettings(null);
			}
			if (proxy == null) {
				// Proxy cannot be null
				proxy = CreateProxySettings(null);
			}
			if (gui == null) {
				// GUI cannot be null
				gui = CreateGUISettings(null);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.InitialSetupDone = initialSetupDone;
				this.LogLevel = logLevel;
				this.Culture = culture;
				this.NoLogo = noLogo;
				this.Credentials = credentials;
				this.SystemSettingsSwitcher = systemSettingsSwitcher;
				this.Proxy = proxy;
				this.GUI = gui;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public CommandSettings(): this(NullObjectData) {
		}

		public CommandSettings(CommandSettings src) : base(src) {
			// argument checks
			if (src == null) {
				throw new ArgumentNullException(nameof(src));
			}

			// clone members
			this.LogLevel = src.LogLevel;
			this.Culture = src.Culture;
			this.NoLogo = src.NoLogo;
			this.Credentials = Clone(src.Credentials);
			this.SystemSettingsSwitcher = Clone(src.SystemSettingsSwitcher);
			this.Proxy = Clone(src.Proxy);
			this.GUI = Clone(src.GUI);

			return;
		}

		#endregion


		#region overrides/overridables

		protected override MAPE.Utils.Settings Clone() {
			return new CommandSettings(this);
		}

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// save settings
			data.SetEnumValue(SettingNames.LogLevel, this.LogLevel, omitDefault, this.LogLevel == Defaults.LogLevel);
			data.SetValue(SettingNames.Culture, this.Culture, CreateCultureInfoValue, omitDefault, Defaults.IsDefaultCulture(this.Culture));
			data.SetBooleanValue(SettingNames.NoLogo, this.NoLogo, omitDefault, this.NoLogo == Defaults.NoLogo);
			data.SetObjectArrayValue(SettingNames.Credentials, this.Credentials, omitDefault, Defaults.IsDefaultCredentials(this.Credentials));
			// SystemSettingsSwitcher: overwrite mode, not omittable (that is, isDefault should be false)
			data.SetObjectValue(SettingNames.SystemSettingsSwitcher, this.SystemSettingsSwitcher, true, omitDefault, false);
			// Proxy: replace mode, not omittable (that is, isDefault should be false)
			data.SetObjectValue(SettingNames.Proxy, this.Proxy, false, omitDefault, false);
			// GUI: overwrite mode, not omittable (that is, isDefault should be false)
			data.SetObjectValue(SettingNames.GUI, this.GUI, true, omitDefault, false);

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

		protected virtual GUISettings CreateGUISettings(IObjectData data) {
			// argument checks
			// data can be null

			return new GUISettings(data);
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
