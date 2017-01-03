using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MAPE.Utils;
using MAPE.Server;


namespace MAPE.Command {
	public static class CommandBaseSettingsExtensions {
		#region methods

		public static CredentialPersistence GetCredentialPersistenceValue(this Settings settings, string settingName, CredentialPersistence defaultValue, bool createIfNotExist = false) {
			return (CredentialPersistence)settings.GetEnumValue(settingName, typeof(CredentialPersistence), defaultValue, createIfNotExist);
		}

		public static void SetCredentialPersistenceValue(this Settings settings, string settingName, CredentialPersistence value, bool omitDefault, CredentialPersistence defaultValue) {
			settings.SetEnumValue(settingName, value, omitDefault, defaultValue);
		}


		public static Settings GetProxySettings(this Settings settings, bool createIfNotExist = true) {
			return settings.GetObjectValue(CommandBase.SettingNames.Proxy, Settings.EmptySettingsGenerator, createIfNotExist);
		}

		public static Settings GetSystemSettingSwitchSettings(this Settings settings, bool createIfNotExist = true) {
			return settings.GetObjectValue(CommandBase.SettingNames.SystemSettingSwitch, Settings.EmptySettingsGenerator, createIfNotExist);
		}

		#endregion
	}


	public abstract class CommandBase: IDisposable {
		#region types

		public static class OptionNames {
			#region constants

			public const string Help = "Help";

			public const string SettingsFile = "SettingsFile";

			public const string NoSettings = "NoSettings";

			public const string CredentialPersistence = "CredentialPersistence";

			public const string UserName = "UserName";

			public const string Password = "Password";

			public const string MainListener = "MainListener";

			public const string AdditionalListeners = "AdditionalListeners";

			public const string Server = "Server";

			public const string RetryCount = "RetryCount";

			#endregion
		}

		public static class SettingNames {
			#region constants

			public const string CredentialPersistence = OptionNames.CredentialPersistence;

			public const string UserName = OptionNames.UserName;

			public const string ProtectedPassword = "ProtectedPassword";

			public const string Proxy = "Proxy";

			public const string SystemSettingSwitch = "SystemSettingSwitch";

			#endregion
		}

		public class CommandKind {
			#region constants

			public const string RunProxy = "RunProxy";

			public const string ShowUsage = "ShowUsage";

			#endregion
		}

		#endregion


		#region constants

		public const CredentialPersistence DefaultCredentialPersistence = CredentialPersistence.Process;

		#endregion


		#region data

		private static readonly byte[] entropy = Encoding.UTF8.GetBytes("認証プロキシ爆発しろ");


		public readonly ComponentFactory ComponentFactory;

		protected string SettingsFilePath { get; set; } = null;

		protected CredentialPersistence CredentialPersistence { get; set; } = DefaultCredentialPersistence;

		protected NetworkCredential Credential { get; set; } = null;

		protected string Kind { get; set; } = CommandKind.RunProxy;

		#endregion


		#region creation and disposal

		public CommandBase(ComponentFactory componentFactory) {
			// argument checks
			if (componentFactory == null) {
				// use default one
				componentFactory = new ComponentFactory();
			}

			// initialize members
			this.ComponentFactory = componentFactory;

			return;
		}

		public virtual void Dispose() {
			// clear members
			this.Kind = null;
			this.SettingsFilePath = null;

			return;
		}

		#endregion


		#region methods

		public static bool AreSameOptionNames(string name1, string name2) {
			// option names are case-insensitive and not localized
			return StringComparer.OrdinalIgnoreCase.Compare(name1, name2) == 0;
		}

		public static Dictionary<string, string> CreateEmptyOptions() {
			// option names are case-insensitive and not localized
			return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}

		protected Settings LoadSettingsFromFile(string settingsFilePath = null) {
			// argument checks
			if (settingsFilePath == null) {
				settingsFilePath = EnsureSettingsFilePathSet();
			}

			// load settings from the file
			return Settings.Load(settingsFilePath);
		}

		protected void SaveSettingsToFile(Settings settings, string settingsFilePath = null) {
			// argument checks
			if (settings.IsNull) {
				// null setting is not valid
				throw new ArgumentNullException(nameof(settings));
			}
			if (settingsFilePath == null) {
				settingsFilePath = EnsureSettingsFilePathSet();
			}

			settings.Save(settingsFilePath);
		}

		protected RunningProxyState StartProxy(Settings settings, IProxyRunner proxyRunner) {
			// argument checks
			if (proxyRunner == null) {
				throw new ArgumentNullException(nameof(proxyRunner));
			}

			// initialize credential
			CredentialPersistence credentialPersistence = settings.GetCredentialPersistenceValue(SettingNames.CredentialPersistence, defaultValue: CredentialPersistence.Process);
			string userName = settings.GetStringValue(SettingNames.UserName, defaultValue: null);
			string protectedPassword = settings.GetStringValue(SettingNames.ProtectedPassword, defaultValue: null);
			string password = string.IsNullOrEmpty(protectedPassword) ? null : UnprotectPassword(protectedPassword);
			NetworkCredential credential = (userName == null && password == null) ? null : new NetworkCredential(userName, password);
			SetCredential(credentialPersistence, credential, saveIfNecessary: false);

			// get setting valuses to be used
			Settings systemSettingSwitchSettings = settings.GetObjectValue(SettingNames.SystemSettingSwitch);
			Settings proxySettings = settings.GetObjectValue(SettingNames.Proxy);

			// create a RunningProxyState and start the proxy
			RunningProxyState state = this.ComponentFactory.CreateRunningProxyState(this, systemSettingSwitchSettings);
			try {
				state.Start(proxySettings, proxyRunner);
			} catch {
				state.Dispose();
				throw;
			}

			return state;
		}

		protected void SetCredential(CredentialPersistence credentialPersistence, NetworkCredential credential, bool saveIfNecessary) {
			// argument checks
			// credential can be null

			// update CredentialPersistence
			bool persistenceChanged = (this.CredentialPersistence != credentialPersistence);
			this.CredentialPersistence = credentialPersistence;

			// update Credential
			switch (credentialPersistence) {
				case CredentialPersistence.Session:
					// do not keep the credential
					this.Credential = null;
					break;
				case CredentialPersistence.Process:
					// keep the credential
					this.Credential = credential;
					break;
				case CredentialPersistence.Persistent:
					bool credentialChanged = (credential == this.Credential);
					if (credentialChanged) {
						this.Credential = credential;
					}
					if (saveIfNecessary && (credentialChanged || persistenceChanged)) {
						Action action = () => {
							Settings settings;
							try {
								settings = LoadSettingsFromFile();
							} catch {
								settings = Settings.CreateEmptySettings();
							}

							string userName = credential?.UserName;
							string password = credential?.Password;
							string protectedPassword = (password == null) ? null : ProtectPassword(password);
							settings.SetCredentialPersistenceValue(SettingNames.CredentialPersistence, credentialPersistence, false, CredentialPersistence.Process);
							settings.SetStringValue(SettingNames.UserName, userName, omitDefault: true, defaultValue: null);
							settings.SetStringValue(SettingNames.ProtectedPassword, protectedPassword, omitDefault: true, defaultValue: null);
							SaveSettingsToFile(settings);
						};
						Task.Run(action);
					}
					break;
				default:
					throw new ArgumentException("invalid value", nameof(credentialPersistence));
			}

			return;
		}

		#endregion


		#region overridables - argument processing

		protected virtual Settings ProcessArguments(string[] args) {
			// argument checks
			Debug.Assert(args != null);

			// assort arguments into options or normal arguments
			Dictionary<string, string> options = CreateEmptyOptions();
			List<string> normalArguments = new List<string>();

			using (IEnumerator<string> argEnumerator = ((IEnumerable<string>)args).GetEnumerator()) {
				while (argEnumerator.MoveNext()) {
					AssortArgument(argEnumerator, options, normalArguments);
				}
			}

			// create settings from the arguments
			return CreateSettings(options, normalArguments);
		}

		protected virtual void AssortArgument(IEnumerator<string> argEnumerator, IDictionary<string, string> options, IList<string> normalArguments) {
			// argument checks
			Debug.Assert(argEnumerator != null);
			string arg = argEnumerator.Current;
			if (string.IsNullOrEmpty(arg)) {
				// ignore
				return;
			}

			// assort the current argument into a normal argument or an option
			string name;
			string value;
			if (IsOption(argEnumerator, out name, out value)) {
				options.Add(name, value);
			} else {
				normalArguments.Add(arg);
			}

			return;
		}

		protected virtual bool IsOption(IEnumerator<string> argEnumerator, out string name, out string value) {
			// argument checks
			Debug.Assert(argEnumerator != null);
			string arg = argEnumerator.Current;
			Debug.Assert(string.IsNullOrEmpty(arg) == false);

			// By default, options are supporsed to be in the form of "/name:value" or "-name:value".
			// No need to feed the next arg by a argEnumerator.MoveNext() call in this form.
			char firstChar = arg[0];
			if (firstChar != '/' && firstChar != '-') {
				// a normal argument
				name = null;
				value = null;

				return false;
			} else {
				// an option
				int separatorIndex = arg.IndexOf(':', 1);
				if (0 <= separatorIndex) {
					// "/name:value" form
					Debug.Assert(1 <= separatorIndex);
					name = arg.Substring(1, separatorIndex - 1);
					value = arg.Substring(separatorIndex + 1);
				} else {
					// "/name" form
					name = arg.Substring(1);
					value = string.Empty;
				}

				return true;
			}
		}

		protected virtual Settings CreateSettings(IDictionary<string, string> options, IList<string> normalArguments) {
			// argument checks
			Debug.Assert(options != null);
			Debug.Assert(normalArguments != null);

			// create base settings
			Settings settings = Settings.NullSettings;
			bool noSetting = options.ContainsKey(OptionNames.NoSettings);
			if (noSetting == false) {
				// load the settings

				// find the settings file path 
				string settingsFilePath;
				if (options.TryGetValue(OptionNames.SettingsFile, out settingsFilePath) == false) {
					// default location is %LOCALAPPDATA%\MAPE
					string settingsFolderPath = Path.Combine(
						Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
						"MAPE"
					);
					settingsFilePath = Path.Combine(settingsFolderPath, "MAPE.settings");
				}
				this.SettingsFilePath = settingsFilePath;

				// load settings from the config file
				if (File.Exists(settingsFilePath) == false) {
					Logger.LogInformation("The settings file is not found.");
				} else {
					try {
						settings = LoadSettingsFromFile(settingsFilePath);
					} catch (Exception exception) {
						Logger.LogError($"Error on loading settings file '{settingsFilePath}': {exception.Message}");
					}
				}
			}
			if (settings.IsNull) {
				settings = Settings.CreateEmptySettings();
			}

			// consolidate the settings and command line options
			foreach (KeyValuePair<string, string> option in options) {
				if (HandleOption(option.Key, option.Value, settings) == false) {
					throw new Exception($"Unrecognized option '{option.Key}'.");
				}
			}
			foreach (string arg in normalArguments) {
				if (HandleArgument(arg, settings) == false) {
					throw new Exception($"Unrecognized argument '{arg}'.");
				}
			}

			return settings;
		}

		protected virtual bool HandleOption(string name, string value, Settings settings) {
			// argument checks
			Debug.Assert(name != null);
			Debug.Assert(settings.IsNull == false);

			// handle option
			bool handled = true;
			if (AreSameOptionNames(name, OptionNames.Help) || AreSameOptionNames(name, "?")) {
				this.Kind = CommandKind.ShowUsage;
			} else if (AreSameOptionNames(name, OptionNames.SettingsFile) || AreSameOptionNames(name, OptionNames.NoSettings)) {
				// ignore, it was already handled in CreateSettings()
			} else if (AreSameOptionNames(name, OptionNames.CredentialPersistence)) {
				this.CredentialPersistence = (CredentialPersistence)Enum.Parse(typeof(CredentialPersistence), value);
			} else if (AreSameOptionNames(name, OptionNames.UserName)) {
				settings.SetStringValue(SettingNames.UserName, value);
			} else if (AreSameOptionNames(name, OptionNames.Password)) {
				settings.SetStringValue(SettingNames.ProtectedPassword, ProtectPassword(value));
			} else if (AreSameOptionNames(name, OptionNames.MainListener)) {
				settings.GetProxySettings(createIfNotExist: true).SetJsonValue(Proxy.SettingNames.MainListener, value);
			} else if (AreSameOptionNames(name, OptionNames.AdditionalListeners)) {
				settings.GetProxySettings(createIfNotExist: true).SetJsonValue(Proxy.SettingNames.AdditionalListeners, value);
			} else if (AreSameOptionNames(name, OptionNames.Server)) {
				settings.GetProxySettings(createIfNotExist: true).SetJsonValue(Proxy.SettingNames.Server, value);
			} else if (AreSameOptionNames(name, OptionNames.RetryCount)) {
				settings.GetProxySettings(createIfNotExist: true).SetJsonValue(Proxy.SettingNames.RetryCount, value);
			} else {
				handled = false;	// not handled
			}

			return handled;
		}

		protected virtual bool HandleArgument(string arg, Settings settings) {
			return true;	// ignore normal argument
		}

		#endregion


		#region overridables - execution

		public virtual void Run(string[] args) {
			// argument checks
			if (args == null) {
				throw new ArgumentNullException(nameof(args));
			}

			try {
				// process arguments
				Settings settings = ProcessArguments(args);
				Debug.Assert(settings.IsNull == false);

				// execute command based on settings
				Execute(this.Kind, settings);
			} catch (Exception exception) {
				// ToDo: Error Message
				Console.Error.WriteLine(exception.Message);
			}

			return;
		}

		public virtual void Execute(string commandKind, Settings settings) {
			// argument checks
			Debug.Assert(settings.IsNull == false);

			// execute command according to the command kind 
			switch (commandKind) {
				case CommandKind.RunProxy:
					RunProxy(settings);
					break;
				case CommandKind.ShowUsage:
					ShowUsage(settings);
					break;
				default:
					throw new Exception($"Internal Error: Unexpected CommandKind '{commandKind}'");
			}

			return;
		}

		protected virtual void ShowUsage(Settings settings) {
			return;
		}

		protected abstract void RunProxy(Settings settings);

		#endregion


		#region privates

		private string EnsureSettingsFilePathSet() {
			// state checks
			string settingsFilePath = this.SettingsFilePath;
			if (string.IsNullOrEmpty(settingsFilePath)) {
				throw new InvalidOperationException("The settings file is not specified.");
			}

			return settingsFilePath;
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
