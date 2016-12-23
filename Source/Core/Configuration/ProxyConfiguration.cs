using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using MAPE.Utils;
using MAPE.Server;


namespace MAPE.Configuration {
	public class ProxyConfiguration {
		#region types

		public static class Names {
			#region constants

			public const string MainListener = "MainListener";

			public const string AdditionalListeners = "AdditionalListeners";

			public const string Proxy = "Proxy";

			public const string ProxyUserName = "ProxyUserName";

			public const string ProxyCredentialPersistence = "ProxyCredentialPersistence";

			public const string ProtectedProxyPassword = "ProtectedProxyPassword";

			public const string ProxyPassword = "ProxyPassword";

			public const string RetryCount = "RetryCount";

			#endregion
		}

		#endregion


		#region constants

		public const int DefaultRetryCount = 2;		// original try + 2 retries = 3 tries

		#endregion


		#region data

		private static readonly byte[] entropy = Encoding.UTF8.GetBytes("認証プロキシ爆発しろ");


		private ComponentFactory componentFactory;

		public string ConfigFilePath { get; private set; }

		public ListenerConfiguration MainListener { get; set; }

		public ListenerConfiguration[] AdditionalListeners { get; set; }

		public DnsEndPoint Proxy { get; set; }

		public CredentialPersistence ProxyCredentialPersistence { get; set; }

		public string ProxyUserName { get; set; }

		private string protectedProxyPassword;

		private int retryCount;

		#endregion


		#region properties

		public ComponentFactory ComponentFactory {
			get {
				return this.componentFactory;
			}
			set {
				// argument checks
				if (value == null) {
					throw new ArgumentNullException(nameof(value));
				}

				this.componentFactory = value;
			}
		}

		public int RetryCount {
			get {
				return this.retryCount;
			}
			set {
				// argument checks
				if (value < 0) {
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				this.retryCount = value;
			}
		}

		public NetworkCredential ProxyCredential {
			get {
				// unprotect the password
				string protectedPassword = this.protectedProxyPassword;
				string password = (protectedPassword == null) ? null : UnprotectPassword(protectedPassword);

				return new NetworkCredential(this.ProxyUserName, password);
			}
		}

		#endregion


		#region creation and disposal

		public ProxyConfiguration(ComponentFactory componentFactory) {
			// argument checks
			if (componentFactory == null) {
				componentFactory = new ComponentFactory();
			}

			// initialize members
			this.componentFactory = componentFactory;
			this.ConfigFilePath = null;			// use default config file
			this.MainListener = null;			// use default
			this.AdditionalListeners = null;	// no additional listener
			this.Proxy = null;					// not specified
			this.ProxyCredentialPersistence = CredentialPersistence.Process;
			this.ProxyUserName = null;			// no user name
			this.protectedProxyPassword = null; // no password
			this.retryCount = DefaultRetryCount;

			return;
		}

		#endregion


		#region methods

		public void LoadConfiguration(string configFilePath = null) {
			// argument checks
			if (configFilePath == null) {
				// use default config file
				configFilePath = GetDefaultConfigurationFilePath();
			}

			// load settings from the appSettings section in the configuration file
			System.Configuration.Configuration config = OpenConfiguration(configFilePath);
			foreach (KeyValueConfigurationElement appSetting in config.AppSettings.Settings) {
				Parameter setting = new Parameter(appSetting.Key, appSetting.Value);
				if (LoadSetting(setting) == false) {
					Logger.LogWarning($"ConfigurationFile '{configFilePath}': The setting '{setting.Name}' is unrecognized. It is ignored.");
				}
			}

			// update the file path
			this.ConfigFilePath = configFilePath;

			return;
		}

		public void SaveConfiguration(string configFilePath = null, bool saveAs = false) {
			// argument checks
			if (configFilePath == null) {
				configFilePath = this.ConfigFilePath;
				if (configFilePath == null) {
					// no config file opened currently
					throw new InvalidOperationException("No configuration file is opened now.");
				}
			}

			// save settings to the appSettings section in the configuration file
			System.Configuration.Configuration config = OpenConfiguration(configFilePath);
			var appSettings = config.AppSettings.Settings;

			appSettings.Clear();	// ToDo: this emits a clear element in the config file
			WriteSettings((key, value) => { appSettings.Add(key, value); });
			config.Save();

			// update the file path
			if (saveAs) {
				this.ConfigFilePath = configFilePath;
			}

			return;
		}

		public void SetProxyPassword(string value) {
			// argument checks
			// value can null
			// an empty string is normalized to null
			if (string.IsNullOrEmpty(value)) {
				value = null;
			}

			this.protectedProxyPassword = ProtectPassword(value);
		}

		#endregion


		#region overridables

		public virtual string GetDefaultConfigurationFilePath() {
			// %LOCALAPPDATA%\MAPE\mape.config
			string baseFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			return Path.Combine(baseFolderPath, @"MAPE\mape.config");
		}

		public virtual bool LoadSetting(Parameter setting) {
			bool handled = true;
			if (setting.IsName(Names.MainListener)) {
				this.MainListener = ListenerConfiguration.Parse(setting.Value);
			} else if (setting.IsName(Names.AdditionalListeners)) {
				this.AdditionalListeners = ListenerConfiguration.ParseMultiple(setting.Value);
			} else if (setting.IsName(Names.Proxy)) {
				this.Proxy = ListenerConfiguration.ParseDnsEndPoint(setting.Value);
			} else if (setting.IsName(Names.ProxyCredentialPersistence)) {
				this.ProxyCredentialPersistence = (CredentialPersistence)Enum.Parse(typeof(CredentialPersistence), setting.Value, true);
			} else if (setting.IsName(Names.ProxyUserName)) {
				this.ProxyUserName = setting.Value;
			} else if (setting.IsName(Names.ProtectedProxyPassword)) {
				// an empty string is normalize to null
				if (setting.IsNullOrEmptyValue == false) {
					this.protectedProxyPassword = setting.Value;
				} else {
					this.protectedProxyPassword = null;
				}
				this.ProxyCredentialPersistence = CredentialPersistence.Persistent;
			} else if (setting.IsName(Names.ProxyPassword)) {
				// load only (plain password is not written to configuration file) 
				if (setting.IsNullOrEmptyValue == false) {
					this.protectedProxyPassword = ProtectPassword(setting.Value);
				}
			} else if (setting.IsName(Names.RetryCount)) {
				this.RetryCount = int.Parse(setting.Value);
			} else {
				handled = false;   // not handled
			}

			return handled;
		}

		public virtual void WriteSettings(Action<string, string> adder) {
			// argument checks
			Debug.Assert(adder != null);

			// write settings

			// MainListener
			if (this.MainListener != null) {
				string value = this.MainListener.ToString();
				adder(Names.MainListener, value);
			}

			// AdditionalListeners
			if (this.AdditionalListeners != null) {
				string[] values = (
					from listener in this.AdditionalListeners
					select listener.ToString()
				).ToArray();
				string value = string.Join(";", values);
				adder(Names.AdditionalListeners, value);
			}

			// Proxy
			if (this.Proxy != null) {
				string value = ListenerConfiguration.DnsEndPointToString(this.Proxy);
				adder(Names.Proxy, value);
			}

			// ProxyCredentialPersistence, ProxyUserName, ProtectedProxyPassword
			if (this.ProxyCredentialPersistence == CredentialPersistence.Persistent) {
				if (this.ProxyUserName != null) {
					adder(Names.ProxyUserName, this.ProxyUserName);
				}
				// Note that the plain password should be saved.
				// That is, do not save the 'ProxyPassword' setting. 
				if (this.protectedProxyPassword != null) {
					adder(Names.ProtectedProxyPassword, this.protectedProxyPassword);
				}
			} else {
				adder(Names.ProxyCredentialPersistence, this.ProxyCredentialPersistence.ToString());
			}

			// RetryCount
			if (this.RetryCount != DefaultRetryCount) {
				adder(Names.RetryCount, this.RetryCount.ToString());
			}

			return;
		}

		#endregion


		#region privates

		private static System.Configuration.Configuration OpenConfiguration(string configFilePath) {
			// argument checks
			Debug.Assert(string.IsNullOrEmpty(configFilePath) == false);

			// open configuration
			// We use ExeConfigFile mapping because we need only one-layer config here.
			ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap();
			fileMap.ExeConfigFilename = configFilePath;
			return ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
		}

		private static string ProtectPassword(string password) {
			// argument checks
			Debug.Assert(password != null);

			// encrypt the password by ProtectedData API
			// The password is encrypted with the key of the current user.
			// The protected value transfered from other machine or user cannot be decrypted.
			byte[] bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), entropy, DataProtectionScope.CurrentUser);
			return Convert.ToBase64String(bytes);
		}

		private static string UnprotectPassword(string encryptedPassword) {
			// argument checks
			Debug.Assert(encryptedPassword != null);

			// decrypt the password by ProtectedData API.
			byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(encryptedPassword), entropy, DataProtectionScope.CurrentUser);
			return Encoding.UTF8.GetString(bytes);
		}

		#endregion
	}
}
