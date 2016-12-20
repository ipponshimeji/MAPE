using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
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

			public const string ProtectedProxyPassword = "ProtectedProxyPassword";

			public const string ProxyPassword = "ProxyPassword";

			#endregion
		}

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

		public NetworkCredential ProxyCredential {
			get {
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
			this.ConfigFilePath = null;
			this.MainListener = null;		// use default
			this.AdditionalListeners = null;
			this.Proxy = null;              // auto
			this.ProxyCredentialPersistence = CredentialPersistence.Process;
			this.ProxyUserName = null;
			this.protectedProxyPassword = null;

			return;
		}

		#endregion


		#region methods

		public void LoadConfiguration(string configFilePath = null) {
			// argument checks
			if (configFilePath == null) {
				configFilePath = GetDefaultConfigurationFilePath();
			}

			// load settings from the appSettings section in the configuration file
			System.Configuration.Configuration config = OpenConfiguration(configFilePath);
			foreach (KeyValueConfigurationElement appSetting in config.AppSettings.Settings) {
				Parameter setting = new Parameter(appSetting.Key, appSetting.Value);
				if (LoadSetting(setting) == false) {
					// ToDo: message
					Logger.TraceWarning($"Unknown setting {appSetting.Key}. Ignored.");
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
					throw new InvalidOperationException();
				}
			}

			// save settings to the appSettings section in the configuration file
			System.Configuration.Configuration config = OpenConfiguration(configFilePath);
			var appSettings = config.AppSettings.Settings;

			appSettings.Clear();
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
			if (string.IsNullOrEmpty(value)) {
				value = null;
			}

			this.protectedProxyPassword = ProtectPassword(value);
		}

		#endregion


		#region overridables

		public virtual string GetDefaultConfigurationFilePath() {
			// %APPDATA%\MAPE\mape.config
			string baseFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
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
			} else if (setting.IsName(Names.ProxyUserName)) {
				this.ProxyUserName = setting.Value;
			} else if (setting.IsName(Names.ProtectedProxyPassword)) {
				if (setting.IsNullOrEmptyValue == false) {
					this.protectedProxyPassword = setting.Value;
				} else {
					this.protectedProxyPassword = null;
				}
				this.ProxyCredentialPersistence = CredentialPersistence.Persistent;
			} else if (setting.IsName(Names.ProxyPassword)) {
				if (setting.IsNullOrEmptyValue == false) {
					this.protectedProxyPassword = ProtectPassword(setting.Value);
				}
			} else {
				handled = false;   // not handled
			}

			return handled;
		}

		public virtual void WriteSettings(Action<string, string> add) {
			// argument checks
			Debug.Assert(add != null);

			// write settings
			string value;

			// MainListener
			if (this.MainListener != null) {
				value = this.MainListener.ToString();
				add(Names.MainListener, value);
			}

			// AdditionalListeners
			if (this.AdditionalListeners != null) {
				string[] values = (
					from listener in this.AdditionalListeners
					select listener.ToString()
				).ToArray();
				value = string.Join(";", values);
				add(Names.AdditionalListeners, value);
			}

			// Proxy
			if (this.Proxy != null) {
				value = ListenerConfiguration.DnsEndPointToString(this.Proxy);
				add(Names.Proxy, value);
			}

			// ProxyUserName, ProxyPassword
			if (this.ProxyCredentialPersistence == CredentialPersistence.Persistent) {
				if (this.ProxyUserName != null) {
					add(Names.ProxyUserName, this.ProxyUserName);
				}
				// Note that password should be saved in encrypted mode.
				// That is, do not save the 'ProxyPassword' setting. 
				if (this.protectedProxyPassword != null) {
					add(Names.ProtectedProxyPassword, this.protectedProxyPassword);
				}
			}

			return;
		}

		#endregion


		#region privates

		private static System.Configuration.Configuration OpenConfiguration(string configFilePath) {
			// argument checks
			Debug.Assert(string.IsNullOrEmpty(configFilePath) == false);

			// open configuration
			// We use ExeConfigFile mapping because here we need only one-layer config.
			ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap();
			fileMap.ExeConfigFilename = configFilePath;
			return ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
		}

		private static string ProtectPassword(string password) {
			// argument checks
			Debug.Assert(password != null);

			// encrypt the password by ProtectedData API
			// The encryption key is specific to the current user.
			// The value encrypted by other user does not work.
			byte[] bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), entropy, DataProtectionScope.CurrentUser);
			return Convert.ToBase64String(bytes);
		}

		private static string UnprotectPassword(string encryptedPassword) {
			// argument checks
			Debug.Assert(encryptedPassword != null);

			// decrypt the password by ProtectedData
			byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(encryptedPassword), entropy, DataProtectionScope.CurrentUser);
			return Encoding.UTF8.GetString(bytes);
		}

		#endregion
	}
}
