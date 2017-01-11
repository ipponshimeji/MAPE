using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MAPE;
using MAPE.Utils;
using MAPE.ComponentBase;
using MAPE.Server;
using SettingNames = MAPE.Command.CommandBase.SettingNames;
using CredentialInfo = MAPE.Command.CommandBase.CredentialInfo;


namespace MAPE.Command {
	public static class CommandBaseSettingsExtensions {
		#region data

		private static readonly byte[] entropy = Encoding.UTF8.GetBytes("認証プロキシ爆発しろ");

		#endregion


		#region methods

		public static CredentialPersistence GetCredentialPersistenceValue(this Settings settings, string settingName, CredentialPersistence defaultValue, bool createIfNotExist = false) {
			return (CredentialPersistence)settings.GetEnumValue(settingName, typeof(CredentialPersistence), defaultValue, createIfNotExist);
		}

		public static void SetCredentialPersistenceValue(this Settings settings, string settingName, CredentialPersistence value, bool omitDefault, CredentialPersistence defaultValue) {
			settings.SetEnumValue(settingName, value, omitDefault, defaultValue);
		}


		public static IEnumerable<CredentialInfo> GetCredentialsValue(this Settings settings, string settingName) {
			CredentialInfo[] credentials = null;

			IEnumerable<Settings> credentialsSettings = settings.GetObjectArrayValue(settingName, defaultValue: null);
			if (credentialsSettings != null) {
				credentials = (
					from subSettings in credentialsSettings
					select CreateCredentialInfo(subSettings)
				).ToArray();
			}

			return credentials;
		}

		public static CredentialInfo CreateCredentialInfo(this Settings settings) {
			string endPoint = settings.GetStringValue(SettingNames.EndPoint, defaultValue: string.Empty);
			string userName = settings.GetStringValue(SettingNames.UserName, defaultValue: string.Empty);
			string protectedPassword = settings.GetStringValue(SettingNames.ProtectedPassword, defaultValue: null);
			string password = (protectedPassword == null) ? string.Empty : UnprotectPassword(protectedPassword);
			CredentialPersistence persistence = settings.GetCredentialPersistenceValue(SettingNames.Persistence, defaultValue: CommandBase.DefaultCredentialPersistence);

			return new CredentialInfo(endPoint, userName, password, persistence);
		}

		public static void SetCredentialsValue(this Settings settings, string settingName, IEnumerable<CredentialInfo> value, bool omitDefault) {
			// argument checks
			// value can be null

			// set the array of CredentialInfo settings
			Settings[] settingsArray;
			if (value == null) {
				settingsArray = Settings.EmptySettingsArray;
			} else {
				settingsArray = (
					from credential in value
					where credential != null && credential.Persistence == CredentialPersistence.Persistent
					select GetCredentialInfoSettings(credential, omitDefault)
				).ToArray();
			}
			if (omitDefault && settingsArray.Length <= 0) {
				settings.RemoveValue(settingName);
			} else {
				settings.SetObjectArrayValue(settingName, settingsArray);
			}

			return;
		}

		public static Settings GetCredentialInfoSettings(CredentialInfo value, bool omitDefault) {
			// argument checks
			if (value == null) {
				return Settings.NullSettings;
			}

			// create settings of the CredentialInfo
			Settings settings = Settings.CreateEmptySettings();

			string endPoint = NormalizeNullToEmpty(value.EndPoint);
			string userName = NormalizeNullToEmpty(value.UserName);
			string password = value.Password;
			string protectedPassword = string.IsNullOrEmpty(password)? string.Empty: ProtectPassword(password);

			settings.SetStringValue(SettingNames.EndPoint, endPoint, omitDefault, defaultValue: string.Empty);
			settings.SetStringValue(SettingNames.UserName, userName, omitDefault, defaultValue: string.Empty);
			settings.SetStringValue(SettingNames.ProtectedPassword, protectedPassword, omitDefault, defaultValue: string.Empty);
			settings.SetCredentialPersistenceValue(SettingNames.Persistence, value.Persistence, omitDefault, defaultValue: CommandBase.DefaultCredentialPersistence);

			return settings;
		}


		public static Settings GetProxySettings(this Settings settings, bool createIfNotExist = true) {
			return settings.GetObjectValue(CommandBase.SettingNames.Proxy, Settings.EmptySettingsGenerator, createIfNotExist);
		}

		public static Settings GetSystemSettingSwitcherSettings(this Settings settings, bool createIfNotExist = true) {
			return settings.GetObjectValue(CommandBase.SettingNames.SystemSettingSwitcher, Settings.EmptySettingsGenerator, createIfNotExist);
		}

		#endregion


		#region privates

		private static string NormalizeNullToEmpty(string value) {
			return (value == null) ? string.Empty : value;
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

	public abstract class CommandBase: Component, IProxyRunner {
		#region types

		public static class OptionNames {
			#region constants

			public const string Help = "Help";

			public const string SettingsFile = "SettingsFile";

			public const string NoSettings = "NoSettings";

			public const string Credential = "Credential";

			public const string MainListener = "MainListener";

			public const string AdditionalListeners = "AdditionalListeners";

			public const string RetryCount = "RetryCount";

			public const string EnableSystemSettingSwitch = "EnableSystemSettingSwitch";

			public const string ActualProxy = "ActualProxy";

			#endregion
		}

		public static class SettingNames {
			#region constants

			public const string Credentials = "Credentials";

			public const string EndPoint = "EndPoint";

			public const string UserName = "UserName";

			public const string ProtectedPassword = "ProtectedPassword";

			public const string Persistence = "Persistence";


			public const string Proxy = "Proxy";

			public const string SystemSettingSwitcher = "SystemSettingSwitcher";

			#endregion
		}

		public class CommandKind {
			#region constants

			public const string RunProxy = "RunProxy";

			public const string ShowUsage = "ShowUsage";

			#endregion
		}

		public class CredentialInfo {
			#region data

			private readonly NetworkCredential credential;

			public readonly CredentialPersistence Persistence;

			#endregion


			#region properties

			public string EndPoint {
				get {
					// Note that the endPoint is stored as 'Domain' property of the NetworkCredential object.
					return this.credential.Domain;
				}
			}

			public string UserName {
				get {
					return this.credential.UserName;
				}
			}

			public string Password {
				get {
					return this.credential.Password;
				}
			}

			#endregion


			#region creation and disposal

			public CredentialInfo(string endPoint, string userName, string password, CredentialPersistence persistence) {
				// argument checks
				if (endPoint == null) {
					// endPoint can be empty
					throw new ArgumentNullException(nameof(endPoint));
				}
				// userName can be null
				// password can be null

				// initialize members
				// Note that the endPoint is stored as 'Domain' property of the NetworkCredential object.
				this.credential = new NetworkCredential(userName, password, endPoint);
				this.Persistence = persistence;

				return;
			}

			#endregion


			#region methods

			private static bool AreSameEndPoint(string endPoint1, string endPoint2) {
				return string.Compare(endPoint1, endPoint2, StringComparison.OrdinalIgnoreCase) == 0;
			}

			public NetworkCredential GetNetworkCredential() {
				// return a clone of this.credential not to be changed its contents
				NetworkCredential credential = this.credential;
				return new NetworkCredential(credential.UserName, credential.Password, credential.Domain);
			}

			#endregion


			#region overrides

			public override bool Equals(object obj) {
				// argument checks
				CredentialInfo another = obj as CredentialInfo;
				if (another == null) {
					return false;
				}

				return (
					this.Persistence == another.Persistence &&
					AreSameEndPoint(this.EndPoint, another.EndPoint) &&
					string.CompareOrdinal(this.UserName, another.UserName) == 0 &&
					string.CompareOrdinal(this.Password, another.Password) == 0
				);
			}

			public override int GetHashCode() {
				return this.credential.GetHashCode() ^ this.Persistence.GetHashCode();
			}

			#endregion
		}

		protected class RunningProxyState: IDisposable {
			#region data

			public readonly CommandBase Owner;

			private Proxy proxy = null;

			private SystemSettingsSwitcher backup = null;

			#endregion


			#region creation and disposal

			public RunningProxyState(CommandBase owner) {
				// argument checks
				if (owner == null) {
					throw new ArgumentNullException(nameof(owner));
				}

				// inirialize members
				this.Owner = owner;

				return;
			}

			public virtual void Dispose() {
				// state checks
				if (this.proxy != null) {
					Stop();
				}
				Debug.Assert(this.proxy == null);

				return;
			}

			#endregion


			#region methods

			public void Start(Settings systemSettingsSwitcherSettings, Settings proxySettings, IProxyRunner proxyRunner) {
				// argument checks
				// systemSettingsSwitcherSettings can contain null
				// proxySettings can contain null
				if (proxyRunner == null) {
					throw new ArgumentNullException(nameof(proxyRunner));
				}

				// state checks
				if (this.proxy != null) {
					throw new InvalidOperationException("Already started.");
				}

				try {
					ComponentFactory componentFactory = this.Owner.ComponentFactory;

					// create a proxy
					Proxy proxy = componentFactory.CreateProxy(proxySettings);

					// create a system settings swither
					SystemSettingsSwitcher systemSettingsSwitcher = componentFactory.CreateSystemSettingsSwitcher(this.Owner, systemSettingsSwitcherSettings, proxy);

					// start the proxy
					proxy.ActualProxy = systemSettingsSwitcher.ActualProxy;
					proxy.Start(proxyRunner);
					this.proxy = proxy;

					// switch system settings
					this.backup = systemSettingsSwitcher.Switch(makeBackup: true);
				} catch {
					Stop();
					throw;
				}

				return;
			}

			public bool Stop(int millisecondsTimeout = 0) {
				// restore the system settings
				SystemSettingsSwitcher backup = this.backup;
				this.backup = null;
				if (backup != null) {
					try {
						backup.Switch(makeBackup: false);
					} catch (Exception exception) {
						// ToDo: the way to send the message to owner
						// Console is not appropriate for GUI
						Console.Error.Write($"Fail to restore the previous system settings: {exception.Message}");
						Console.Error.Write("Please restore it manually.");
						// continue
					}
				}

				// stop and dispose the proxy
				Proxy proxy = this.proxy;
				this.proxy = null;
				bool stopConfirmed = false;
				if (proxy == null) {
					stopConfirmed = true;
				} else {
					try {
						stopConfirmed = proxy.Stop(millisecondsTimeout);
					} finally {
						proxy.Dispose();
					}
				}

				return stopConfirmed;
			}

			#endregion
		}

		#endregion


		#region constants

		public const CredentialPersistence DefaultCredentialPersistence = CredentialPersistence.Persistent;

		#endregion


		#region data

		public readonly ComponentFactory ComponentFactory;

		protected string SettingsFilePath { get; set; } = null;

		protected IDictionary<string, CredentialInfo> Credentials { get; private set; } = new Dictionary<string, CredentialInfo>();

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

		public override void Dispose() {
			// clear members
			this.Kind = null;
			this.Credentials = null;
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

			// get setting valuses to be used
			Settings systemSettingSwitcherSettings = settings.GetObjectValue(SettingNames.SystemSettingSwitcher);
			Settings proxySettings = settings.GetObjectValue(SettingNames.Proxy);

			// create a RunningProxyState and start the proxy
			RunningProxyState state = new RunningProxyState(this);
			try {
				state.Start(systemSettingSwitcherSettings, proxySettings, proxyRunner);
			} catch {
				state.Dispose();
				throw;
			}

			return state;
		}

		protected void SetCredential(CredentialInfo credential, bool saveIfNecessary) {
			// argument checks
			if (credential == null) {
				throw new ArgumentNullException(nameof(credential));
			}

			lock (this) {
				// state checks
				IDictionary<string, CredentialInfo> credentials = this.Credentials;
				if (credential == null) {
					throw new ObjectDisposedException(null);
				}

				// register the credential to the this.Credentials
				string endPoint = credential.EndPoint;
				bool changed = false;
				CredentialInfo oldCredential;
				if (credentials.TryGetValue(endPoint, out oldCredential)) {
					// the credential for the endpoint exists
					changed = !credential.Equals(oldCredential);
				} else {
					// newly added
					changed = true;
				}
				if (changed) {
					// register the credential
					if (credential.Persistence == CredentialPersistence.Session) {
						credentials.Remove(endPoint);
					} else {
						credentials[endPoint] = credential;
					}

					// update settings file if necessary
					if (saveIfNecessary) {
						// create a clone of the credential list
						CredentialInfo[] credentialArray = credentials.Values.ToArray(); 
						Action action = () => {
							Settings settings;
							try {
								settings = LoadSettingsFromFile();
							} catch {
								settings = Settings.CreateEmptySettings();
							}

							settings.SetCredentialsValue(SettingNames.Credentials, credentialArray, omitDefault: true);
							SaveSettingsToFile(settings);
						};

						// launch save task
						Task.Run(action);
					}
				}
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
					settingsFilePath = Path.Combine(settingsFolderPath, "Settings.json");
				}
				this.SettingsFilePath = settingsFilePath;

				// load settings from the config file
				if (File.Exists(settingsFilePath) == false) {
					Logger.LogInformation("The settings file is not found. Assuming empty one.");
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

			// load the settings
			LoadSettings(settings);

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

		protected virtual void LoadSettings(Settings settings) {
			// argument checks
			// settings can contain null

			// SettingNames.Credentials
			IEnumerable<CredentialInfo> credentials = settings.GetCredentialsValue(SettingNames.Credentials);
			if (credentials != null) {
				IDictionary<string, CredentialInfo> dictionary = this.Credentials;
				foreach (CredentialInfo credential in credentials) {
					dictionary.Add(credential.EndPoint, credential);
				}
			}

			return;
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
			} else if (AreSameOptionNames(name, OptionNames.Credential)) {
				CredentialInfo credential = Settings.Parse(value).CreateCredentialInfo();
				SetCredential(credential, saveIfNecessary: false);
			} else if (AreSameOptionNames(name, OptionNames.MainListener)) {
				settings.GetProxySettings(createIfNotExist: true).SetJsonValue(Proxy.SettingNames.MainListener, value);
			} else if (AreSameOptionNames(name, OptionNames.AdditionalListeners)) {
				settings.GetProxySettings(createIfNotExist: true).SetJsonValue(Proxy.SettingNames.AdditionalListeners, value);
			} else if (AreSameOptionNames(name, OptionNames.RetryCount)) {
				settings.GetProxySettings(createIfNotExist: true).SetJsonValue(Proxy.SettingNames.RetryCount, value);
			} else if (AreSameOptionNames(name, OptionNames.EnableSystemSettingSwitch)) {
				settings.GetSystemSettingSwitcherSettings(createIfNotExist: true).SetJsonValue(SystemSettingsSwitcher.SettingNames.EnableSystemSettingSwitch, value);
			} else if (AreSameOptionNames(name, OptionNames.ActualProxy)) {
				settings.GetSystemSettingSwitcherSettings(createIfNotExist: true).SetJsonValue(SystemSettingsSwitcher.SettingNames.ActualProxy, value);
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

		protected virtual CredentialInfo UpdateCredential(string endPoint, string realm, CredentialInfo oldCredential) {
			return null;	// no credential by default
		}

		#endregion


		#region IProxyRunner - for Proxy class only

		ValueTuple<NetworkCredential, bool> IProxyRunner.GetCredential(string endPoint, string realm, bool needUpdate) {
			// argument checks
			if (endPoint == null) {
				throw new ArgumentNullException(nameof(endPoint));
			}
			if (realm == null) {
				realm = string.Empty;
			}

			// lock to share the user response via console.
			CredentialInfo credential = null;
			lock (this) {
				// state checks
				IDictionary<string, CredentialInfo> credentials = this.Credentials;
				if (credentials == null) {
					throw new ObjectDisposedException(null);
				}

				// try to find the credential for the end point
				if (credentials.TryGetValue(endPoint, out credential) == false) {
					// try to find the credential for the "wildcard"
					if (credentials.TryGetValue(string.Empty, out credential) == false) {
						needUpdate = true;
					}
				}

				// update the credential if necessary
				if (needUpdate) {
					credential = UpdateCredential(endPoint, realm, credential);
					if (credential != null) {
						SetCredential(credential, saveIfNecessary: true);
					}
				}
			}

			// return the clone of this.Credential
			return new ValueTuple<NetworkCredential, bool>(credential.GetNetworkCredential(), credential.Persistence != CredentialPersistence.Session);
		}

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

		#endregion
	}
}
