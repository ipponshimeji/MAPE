using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MAPE.Utils;
using MAPE.ComponentBase;
using MAPE.Server;
using MAPE.Properties;
using SettingNames = MAPE.Command.CommandBase.SettingNames;


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

		public static TraceLevel GetTraceLevelValue(this Settings settings, string settingName, TraceLevel defaultValue, bool createIfNotExist = false) {
			return (TraceLevel)settings.GetEnumValue(settingName, typeof(TraceLevel), defaultValue, createIfNotExist);
		}

		public static void SetTraceLevelValue(this Settings settings, string settingName, TraceLevel value, bool omitDefault, TraceLevel defaultValue) {
			settings.SetEnumValue(settingName, value, omitDefault, defaultValue);
		}

		public static CultureInfo GetCultureInfoValue(this Settings settings, string settingName, CultureInfo defaultValue, bool createIfNotExist = false) {
			string value = settings.GetStringValue(settingName, null);
			return (value == null) ? null : new CultureInfo(value);
		}

		public static IEnumerable<CredentialInfo> GetCredentialsValue(this Settings settings, string settingName) {
			CredentialInfo[] credentials = null;

			// the value is an array of CredentialInfo object
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
			string password = string.IsNullOrEmpty(protectedPassword) ? string.Empty : UnprotectPassword(protectedPassword);
			CredentialPersistence persistence = settings.GetCredentialPersistenceValue(SettingNames.Persistence, defaultValue: CommandBase.DefaultCredentialPersistence);
			bool enableAssumptionMode = settings.GetBooleanValue(SettingNames.EnableAssumptionMode, defaultValue: true);

			return new CredentialInfo(endPoint, userName, password, persistence, enableAssumptionMode);
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

			string endPoint = value.EndPoint ?? string.Empty;
			string userName = value.UserName ?? string.Empty;
			string password = value.Password;
			string protectedPassword = string.IsNullOrEmpty(password)? string.Empty: ProtectPassword(password);

			settings.SetStringValue(SettingNames.EndPoint, endPoint, omitDefault, defaultValue: string.Empty);
			settings.SetStringValue(SettingNames.UserName, userName, omitDefault, defaultValue: string.Empty);
			settings.SetStringValue(SettingNames.ProtectedPassword, protectedPassword, omitDefault, defaultValue: string.Empty);
			settings.SetCredentialPersistenceValue(SettingNames.Persistence, value.Persistence, omitDefault, defaultValue: CommandBase.DefaultCredentialPersistence);
			settings.SetBooleanValue(SettingNames.EnableAssumptionMode, value.EnableAssumptionMode, omitDefault, defaultValue: true);

			return settings;
		}


		public static Settings GetProxySettings(this Settings settings, bool createIfNotExist = true) {
			return settings.GetObjectValue(CommandBase.SettingNames.Proxy, Settings.EmptySettingsGenerator, createIfNotExist);
		}

		public static Settings GetSystemSettingSwitcherSettings(this Settings settings, bool createIfNotExist = true) {
			return settings.GetObjectValue(CommandBase.SettingNames.SystemSettingsSwitcher, Settings.EmptySettingsGenerator, createIfNotExist);
		}

		#endregion


		#region privates

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

			public const string Culture = SettingNames.Culture;

			public const string LogLevel = SettingNames.LogLevel;

			public const string Credential = "Credential";

			public const string MainListener = Proxy.SettingNames.MainListener;

			public const string AdditionalListeners = Proxy.SettingNames.AdditionalListeners;

			public const string RetryCount = Proxy.SettingNames.RetryCount;

			public const string EnableSystemSettingsSwitch = SystemSettingsSwitcher.SettingNames.EnableSystemSettingsSwitch;

			public const string ActualProxy = SystemSettingsSwitcher.SettingNames.ActualProxy;

			#endregion
		}

		public static class SettingNames {
			#region constants

			public const string LogLevel = "LogLevel";

			public const string Culture = "Culture";

			public const string Credentials = "Credentials";

			public const string EndPoint = "EndPoint";

			public const string UserName = "UserName";

			public const string ProtectedPassword = "ProtectedPassword";

			public const string Persistence = "Persistence";

			public const string EnableAssumptionMode = "EnableAssumptionMode";


			public const string Proxy = "Proxy";

			public const string SystemSettingsSwitcher = "SystemSettingsSwitcher";

			#endregion
		}

		public class ExecutionKind {
			#region constants

			public const string RunProxy = "RunProxy";

			public const string ShowUsage = "ShowUsage";

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
					throw new InvalidOperationException("The proxy is already started.");
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
						string message = string.Format(Resources.RunningProxyState_Message_FailToRestoreSystemSettings, exception.Message);
						this.Owner.ShowErrorMessage(message);
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
						Util.DisposeWithoutFail(proxy);
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


		// following data are not changed after execution starts (inside Execute() method)

		protected string SettingsFilePath { get; set; } = null;

		protected string Kind { get; set; } = ExecutionKind.RunProxy;

		#endregion


		#region data - data synchronized by credentialsLocker

		private readonly object credentialsLocker = new object();

		private Dictionary<string, CredentialInfo> credentials = new Dictionary<string, CredentialInfo>();

		#endregion


		#region properties

		protected bool HasSettingsFile {
			get {
				return string.IsNullOrEmpty(this.SettingsFilePath) == false;
			}
		}

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
			this.credentials = null;
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

		protected Settings LoadSettingsFromFile(bool createIfNotExist, string settingsFilePath = null) {
			// argument checks
			if (settingsFilePath == null) {
				settingsFilePath = EnsureSettingsFilePathSet();
			}

			// load settings from the file
			return Settings.Load(settingsFilePath, createIfNotExist);
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

			// save settings to the file
			settings.Save(settingsFilePath);
		}

		protected RunningProxyState StartProxy(Settings settings, IProxyRunner proxyRunner) {
			// argument checks
			if (proxyRunner == null) {
				throw new ArgumentNullException(nameof(proxyRunner));
			}

			// state checks
			if (this.credentials == null) {
				throw CreateObjectDisposedException();
			}

			// get setting valuses to be used
			Settings systemSettingSwitcherSettings = settings.GetObjectValue(SettingNames.SystemSettingsSwitcher);
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

			lock (this.credentialsLocker) {
				// state checks
				IDictionary<string, CredentialInfo> credentials = this.credentials;
				if (credential == null) {
					throw CreateObjectDisposedException();
				}

				// register the credential to the credential list
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
						string settingsFilePath = this.SettingsFilePath;
						if (string.IsNullOrEmpty(settingsFilePath) == false) {
							// create a clone of the credential list
							CredentialInfo[] credentialArray = credentials.Values.ToArray();
							Action saveTask = () => {
								try {
									Settings settings = LoadSettingsFromFile(false, settingsFilePath);

									settings.SetCredentialsValue(SettingNames.Credentials, credentialArray, omitDefault: true);
									SaveSettingsToFile(settings, settingsFilePath);
								} catch (Exception exception) {
									string message = string.Format(Resources.CommandBase_Message_FailToSaveCredentials, exception.Message);
									ShowErrorMessage(message);
								}
							};

							// launch save task
							Task.Run(saveTask);
						} else {
							string message = string.Format(Resources.CommandBase_Message_FailToSaveCredentials, Resources.CommandBase_Message_NoSettingsFile);
							ShowErrorMessage(message);
						}
					}
				}
			}

			return;
		}


		protected void LogProxyStarted(bool resuming) {
			if (resuming) {
				LogResume("Proxy started due to system resuming.");
			} else {
				LogStart("Proxy started.");
			}
		}

		protected void LogProxyStopped(bool completed, bool suspending) {
			string baseMessage = completed ? "Proxy stopped" : "Proxy is stopping";
			if (suspending) {
				LogSuspend($"{baseMessage} due to system suspending.");
			} else {
				LogStop($"{baseMessage}.");
			}
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
			return CreateExecutingSettings(options, normalArguments);
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

		protected virtual Settings CreateExecutingSettings(IDictionary<string, string> options, IList<string> normalArguments) {
			// argument checks
			Debug.Assert(options != null);
			Debug.Assert(normalArguments != null);

			// create base settings
			Settings settings = GetBaseSettings(options);

			// load the base settings
			LoadBaseSettings(settings);

			// consolidate the settings and command line options
			foreach (KeyValuePair<string, string> option in options) {
				if (HandleOption(option.Key, option.Value, settings) == false) {
					string message = string.Format(Resources.CommandBase_Message_InvalidOption, option.Key);
					throw new Exception(message);
				}
			}
			foreach (string arg in normalArguments) {
				if (HandleArgument(arg, settings) == false) {
					string message = string.Format(Resources.CommandBase_Message_InvalidArgument, arg);
					throw new Exception(message);
				}
			}

			return settings;
		}

		protected virtual void LoadBaseSettings(Settings settings) {
			// argument checks
			// settings can contain null

			// SettingNames.Credentials
			IEnumerable<CredentialInfo> credentials = settings.GetCredentialsValue(SettingNames.Credentials);
			if (credentials != null) {
				IDictionary<string, CredentialInfo> dictionary = this.credentials;
				dictionary.Clear();
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
				this.Kind = ExecutionKind.ShowUsage;
			} else if (AreSameOptionNames(name, OptionNames.SettingsFile) || AreSameOptionNames(name, OptionNames.NoSettings)) {
				// ignore, it was already handled in CreateSettings()
			} else if (AreSameOptionNames(name, OptionNames.LogLevel)) {
				settings.SetStringValue(SettingNames.LogLevel, value);
			} else if (AreSameOptionNames(name, OptionNames.Culture)) {
				settings.SetStringValue(SettingNames.Culture, value);
			} else if (AreSameOptionNames(name, OptionNames.Credential)) {
				CredentialInfo credential = Settings.Parse(value).CreateCredentialInfo();
				SetCredential(credential, saveIfNecessary: false);
			} else if (AreSameOptionNames(name, OptionNames.MainListener)) {
				settings.GetProxySettings(createIfNotExist: true).SetJsonValue(Proxy.SettingNames.MainListener, value);
			} else if (AreSameOptionNames(name, OptionNames.AdditionalListeners)) {
				settings.GetProxySettings(createIfNotExist: true).SetJsonValue(Proxy.SettingNames.AdditionalListeners, value);
			} else if (AreSameOptionNames(name, OptionNames.RetryCount)) {
				settings.GetProxySettings(createIfNotExist: true).SetJsonValue(Proxy.SettingNames.RetryCount, value);
			} else if (AreSameOptionNames(name, OptionNames.EnableSystemSettingsSwitch)) {
				settings.GetSystemSettingSwitcherSettings(createIfNotExist: true).SetJsonValue(SystemSettingsSwitcher.SettingNames.EnableSystemSettingsSwitch, value);
			} else if (AreSameOptionNames(name, OptionNames.ActualProxy)) {
				settings.GetSystemSettingSwitcherSettings(createIfNotExist: true).SetJsonValue(SystemSettingsSwitcher.SettingNames.ActualProxy, value);
			} else {
				handled = false;	// not handled
			}

			return handled;
		}

		protected virtual bool HandleArgument(string arg, Settings settings) {
			return false;
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
				Settings settings = Settings.NullSettings;
				try {
					settings = ProcessArguments(args);
					Debug.Assert(settings.IsNull == false);
				} catch (Exception exception) {
					// show usage
					ShowErrorMessage(exception.Message);
					this.Kind = ExecutionKind.ShowUsage;
				}

				// set culture
				CultureInfo culture = settings.GetCultureInfoValue(SettingNames.Culture, null);
				if (culture != null) {
					Thread.CurrentThread.CurrentCulture = culture;
					Thread.CurrentThread.CurrentUICulture = culture;
				}

				// set log level
				TraceLevel logLevel = settings.GetTraceLevelValue(SettingNames.LogLevel, defaultValue: Logger.LogLevel);
				Logger.LogLevel = logLevel;

				// execute command based on the settings
				Execute(this.Kind, settings);
			} catch (Exception exception) {
				ShowErrorMessage(exception.Message);
			} finally {
				Logger.StopLogging(1000);
			}

			return;
		}

		public virtual void Execute(string commandKind, Settings settings) {
			// argument checks
			Debug.Assert(commandKind != null);

			// execute command according to the command kind 
			switch (commandKind) {
				case ExecutionKind.RunProxy:
					RunProxy(settings);
					break;
				case ExecutionKind.ShowUsage:
					ShowUsage(settings);
					break;
				default:
					throw new Exception($"Internal Error: Unexpected ExecutionKind '{commandKind}'");
			}

			return;
		}

		protected abstract void ShowUsage(Settings settings);

		protected abstract void RunProxy(Settings settings);

		protected virtual CredentialInfo UpdateCredential(string endPoint, string realm, CredentialInfo oldCredential) {
			return null;	// no credential by default
		}

		#endregion


		#region overridables - misc

		protected abstract void ShowErrorMessage(string message);

		#endregion


		#region IProxyRunner - for Proxy class only

		CredentialInfo IProxyRunner.GetCredential(string endPoint, string realm, bool needUpdate) {
			// argument checks
			if (endPoint == null) {
				throw new ArgumentNullException(nameof(endPoint));
			}
			if (realm == null) {
				realm = string.Empty;
			}

			// Note lock is needed not only to access this.Credentials but also to share the user response
			CredentialInfo credential = null;
			lock (this.credentialsLocker) {
				// state checks
				IDictionary<string, CredentialInfo> credentials = this.credentials;
				if (credentials == null) {
					throw CreateObjectDisposedException();
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
						SetCredential(credential, saveIfNecessary: this.HasSettingsFile);
					}
				}
			}

			// return the clone of the credential not to be changed
			return credential.Clone();
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

		private Settings GetBaseSettings(IDictionary<string, string> options) {
			// argument checks
			Debug.Assert(options != null);

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

				// load settings from the config file
				try {
					settings = LoadSettingsFromFile(true, settingsFilePath);
					this.SettingsFilePath = settingsFilePath;
				} catch (Exception exception) {
					string message = string.Format(Resources.CommandBase_Message_FailToLoadSettingsFile, settingsFilePath, exception.Message);
					ShowErrorMessage(message);
				}
			}
			if (settings.IsNull) {
				settings = Settings.CreateEmptySettings();
			}

			return settings;
		}

		#endregion
	}
}
