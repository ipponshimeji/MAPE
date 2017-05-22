using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MAPE.Utils;
using MAPE.ComponentBase;
using MAPE.Server;
using MAPE.Properties;
using MAPE.Command.Settings;
using MAPE.Server.Settings;


namespace MAPE.Command {
	public abstract class CommandBase: Component {
		#region types

		public static class OptionNames {
			#region constants

			public const string Help = "Help";

			public const string SettingsFile = "SettingsFile";

			public const string NoSettings = "NoSettings";

			public const string Culture = CommandSettings.SettingNames.Culture;

			public const string LogLevel = CommandSettings.SettingNames.LogLevel;

			public const string Credential = "Credential";

			public const string MainListener = ProxySettings.SettingNames.MainListener;

			public const string AdditionalListeners = ProxySettings.SettingNames.AdditionalListeners;

			public const string RetryCount = ProxySettings.SettingNames.RetryCount;

			public const string EnableSystemSettingsSwitch = SystemSettingsSwitcherSettings.SettingNames.EnableSystemSettingsSwitch;

			public const string ActualProxy = SystemSettingsSwitcherSettings.SettingNames.ActualProxy;

			#endregion
		}

		public class ExecutionKind {
			#region constants

			public const string RunProxy = "RunProxy";

			public const string ShowUsage = "ShowUsage";

			#endregion
		}

		protected class RunningProxyState: IDisposable, IProxyRunner {
			#region data

			public readonly CommandBase Owner;

			private CommandSettings commandSettings = null;

			private Proxy proxy = null;

			private SystemSettingsSwitcher switcher = null;

			private SystemSettings backup = null;

			private bool saveCredentials = false;

			#endregion


			#region data - data synchronized by credentialsLocker

			private readonly object credentialsLocker = new object();

			private Dictionary<string, CredentialSettings> dictionary = null;

			private bool isCredentialsDirty = false;

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
					Stop(systemSessionEnding: false);
				}
				Debug.Assert(this.proxy == null);
				Debug.Assert(this.dictionary == null);

				return;
			}

			#endregion


			#region methods

			public void Start(CommandSettings commandSettings, bool saveCredentials, bool checkPreviousBackup) {
				// argument checks
				if (commandSettings == null) {
					throw new ArgumentNullException(nameof(commandSettings));
				}
				SystemSettingsSwitcherSettings systemSettingsSwitcherSettings = commandSettings.SystemSettingsSwitcher;
				if (systemSettingsSwitcherSettings == null) {
					throw new ArgumentNullException(nameof(commandSettings.SystemSettingsSwitcher));
				}
				ProxySettings proxySettings = commandSettings.Proxy;
				if (proxySettings == null) {
					throw new ArgumentNullException(nameof(commandSettings.Proxy));
				}

				// state checks
				if (this.proxy != null) {
					throw new InvalidOperationException("The proxy is already started.");
				}
				Debug.Assert(this.switcher == null);
				Debug.Assert(this.backup == null);

				try {
					ComponentFactory componentFactory = this.Owner.ComponentFactory;

					// check the state of previous backup
					if (checkPreviousBackup) {
						this.Owner.CheckPreviousBackup();
					}

					// create a system settings swither
					SystemSettingsSwitcher switcher = componentFactory.CreateSystemSettingsSwitcher(this.Owner, systemSettingsSwitcherSettings);
					if (switcher.ActualProxy == null) {
						// no actual proxy to which it connects
						throw new Exception(Resources.CommandBase_Message_NoActualProxy);
					}
					this.switcher = switcher;

					// log
					CommandBase owner = this.Owner;
					if (owner.ShouldLog(TraceEventType.Verbose)) {
						// log the actual proxy if it is static
						DnsEndPoint actualProxyEndPoint = switcher.ActualProxyEndPoint;
						if (actualProxyEndPoint != null) {
							owner.LogVerbose($"ActualProxy is {actualProxyEndPoint.Host}:{actualProxyEndPoint.Port}");
						}

						// log whether system settings is being switched
						string label = switcher.Enabled ? "enabled" : "disabled";
						owner.LogVerbose($"SystemSettingsSwitch: {label}");
					}

					// setup credential dictionary
					lock (this.credentialsLocker) {
						IEnumerable<CredentialSettings> credentials = commandSettings.Credentials;
						this.dictionary = (credentials == null)? new Dictionary<string, CredentialSettings>(): credentials.ToDictionary(c => c.EndPoint);
						this.isCredentialsDirty = false;
					}

					// start the proxy
					Proxy proxy = componentFactory.CreateProxy(proxySettings);
					proxy.ActualProxy = switcher.ActualProxy;
					proxy.Start(this);
					this.proxy = proxy;
					this.saveCredentials = saveCredentials;

					// switch system settings
					this.backup = switcher.Switch(proxy);
					if (this.backup != null) {
						// save backup settings
						owner.SaveSystemSettingsBackup(this.backup);
					}

					this.commandSettings = commandSettings;
				} catch {
					Stop(systemSessionEnding: false);
					throw;
				}

				return;
			}

			public bool Stop(bool systemSessionEnding, int millisecondsTimeout = 0) {
				// restore the system settings
				SystemSettingsSwitcher switcher = this.switcher;
				this.switcher = null;
				SystemSettings backup = this.backup;
				this.backup = null;
				if (backup != null) {
					Debug.Assert(switcher != null);
					try {
						switcher.Restore(backup, systemSessionEnding);
						this.Owner.DeleteSystemSettingsBackup();
					} catch (Exception exception) {
						this.Owner.ShowRestoreSystemSettingsErrorMessage(exception.Message);
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

				// update credentials if necessary
				IEnumerable<CredentialSettings> credentials = null;
				lock (this.credentialsLocker) {
					if (this.isCredentialsDirty) {
						this.isCredentialsDirty = false;
						credentials = this.dictionary.Select(pair => CloneSettings(pair.Value)).ToArray();
					}
					this.dictionary = null;
				}
				if (credentials != null) {
					if (this.commandSettings != null) {
						this.commandSettings.Credentials = credentials;
					}
					if (this.saveCredentials) {
						Action saveTask = () => {
							try {
								this.Owner.UpdateSettingsFile((s) => { s.Credentials = credentials; }, null);
							} catch (Exception exception) {
								string message = string.Format(Resources.CommandBase_Message_FailToSaveCredentials, exception.Message);
								this.Owner.ShowErrorMessage(message);
							}
						};

						// launch save task
						Task.Run(saveTask);
					}
				}
				this.saveCredentials = false;

				// checks
				Debug.Assert(this.proxy == null);
				Debug.Assert(this.switcher == null);
				Debug.Assert(this.backup == null);
				Debug.Assert(this.saveCredentials == false);

				return stopConfirmed;
			}

			#endregion


			#region IProxyRunner

			CredentialSettings IProxyRunner.GetCredential(string endPoint, string realm, bool needUpdate) {
				// argument checks
				if (endPoint == null) {
					throw new ArgumentNullException(nameof(endPoint));
				}
				if (realm == null) {
					realm = string.Empty;
				}

				// Note lock is needed not only to access this.credentials but also to share the user response
				CredentialSettings credential = null;
				lock (this.credentialsLocker) {
					// state checks
					IDictionary<string, CredentialSettings> dictionary = this.dictionary;
					if (dictionary == null) {
						throw new ObjectDisposedException(nameof(RunningProxyState));
					}

					if (needUpdate == false) {
						// try to find the credential for the end point
						if (dictionary.TryGetValue(endPoint, out credential) == false) {
							// try to find the credential for the "wildcard"
							if (dictionary.TryGetValue(string.Empty, out credential) == false) {
								needUpdate = true;
							}
						}
					}

					// update the credential if necessary
					if (needUpdate) {
						credential = this.Owner.UpdateCredential(endPoint, realm, credential);
						if (credential != null) {
							SetCredential(credential);
						}
					} else {
						Debug.Assert(credential != null);
					}
				}

				// return the clone of the credential not to be changed
				return CloneSettings(credential);
			}

			#endregion


			#region privates

			private void SetCredential(CredentialSettings credential) {
				// argument checks
				Debug.Assert(credential != null);

				// state checks
				IDictionary<string, CredentialSettings> dictionary = this.dictionary;
				Debug.Assert(dictionary != null);

				// register the credential to the credential list
				string endPoint = credential.EndPoint;
				bool changed = false;
				CredentialSettings oldCredential;
				if (dictionary.TryGetValue(endPoint, out oldCredential)) {
					// the credential for the endpoint exists
					changed = !credential.Equals(oldCredential);
				} else {
					// newly added
					changed = true;
				}

				if (changed) {
					// register the credential
					this.isCredentialsDirty = true;
					if (credential.Persistence == CredentialPersistence.Session) {
						// do not keep in the process state
						dictionary.Remove(endPoint);
					} else {
						dictionary[endPoint] = credential;
					}
				}

				return;
			}

			#endregion
		}

		#endregion


		#region data

		public readonly ComponentFactory ComponentFactory;

		public int BackupHistory { get; } = 5; 

		// following data are not changed after execution starts (inside Execute() method)

		protected string SettingsFilePath { get; set; } = null;

		protected string Kind { get; set; } = ExecutionKind.RunProxy;

		#endregion


		#region properties

		public bool HasSettingsFile {
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

		public static T CloneSettings<T>(T t) where T: MAPE.Utils.Settings {
			return MAPE.Utils.Settings.Clone(t);
		}

		protected JsonObjectData LoadSettingsFromFile(bool createIfNotExist, string settingsFilePath = null) {
			// argument checks
			if (settingsFilePath == null) {
				settingsFilePath = EnsureSettingsFilePathSet();
			}

			// load settings from the file
			return JsonObjectData.Load(settingsFilePath, createIfNotExist);
		}

		protected int GetBackupHistoryFor(string settingsFilePath) {
			if (settingsFilePath == null || string.Compare(settingsFilePath, this.SettingsFilePath, StringComparison.InvariantCultureIgnoreCase) == 0) {
				// updating the settings file
				return this.BackupHistory;
			} else {
				// exporting settings
				return 0;
			}
		}

		protected void SaveSettingsToFile(CommandSettings settings, string settingsFilePath = null) {
			// argument checks
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}
			int backupHistory = GetBackupHistoryFor(settingsFilePath);
			if (settingsFilePath == null) {
				settingsFilePath = EnsureSettingsFilePathSet();
			}

			// get object data to be saved
			JsonObjectData settingsData = JsonObjectData.CreateEmpty();
			settings.SaveToObjectData(settingsData, true);

			// save settings to the file
			Util.BackupAndSave(settingsFilePath, settingsData.Save, backupHistory);
		}

		protected void UpdateSettingsFile(Action<CommandSettings> updateSettings, string settingsFilePath = null) {
			// argument checks
			if (updateSettings == null) {
				throw new ArgumentNullException(nameof(updateSettings));
			}
			int backupHistory = GetBackupHistoryFor(settingsFilePath);
			if (settingsFilePath == null) {
				settingsFilePath = EnsureSettingsFilePathSet();
			}

			// load settings
			JsonObjectData settingsData = LoadSettingsFromFile(true, settingsFilePath);
			CommandSettings settings = this.ComponentFactory.CreateCommandSettings(settingsData);

			// update settings
			updateSettings(settings);
			settings.SaveToObjectData(settingsData, true);

			// save settings to the file
			Util.BackupAndSave(settingsFilePath, settingsData.Save, backupHistory);
		}


		protected void RunProxy(CommandSettings settings) {
			// argument checks
			Debug.Assert(settings != null);

			// prevent from invoking multiple instances
			// Open shared event object. If it already exists, that means another MAPE instance is running.
			string name = GetForwardingEventName();
			bool createdNew;
			using (EventWaitHandle forwardingEvent = new EventWaitHandle(false, EventResetMode.ManualReset, name, out createdNew)) {
				if (createdNew == false) {
					// Another MAPE instance is running proxy.
					// signal the event to notify another MAPE to move foreground
					ShowErrorMessage(Resources.CommandBase_Message_AnotherInstanceIsRunning);
					forwardingEvent.Set();
					// quit
				} else {
					// this is the only MAPE instance to be running proxy
					// prepare a thread to move this MAPE foreground if the event is signaled
					object locker = new object();
					bool quitting = false;
					Action watch = () => {
						do {
							// wait for the event
							forwardingEvent.WaitOne();
							lock (locker) {
								if (quitting) {
									return;
								}

								// forwarded by another MAPE instance
								BringAppToForeground();
								forwardingEvent.Reset();
							}
						} while (true);
					};
					Task watchingTask = Task.Run(watch);

					// run proxy
					try {
						// check previous backup
						// This check must be done inside the scope in which
						// no other MAPE instance is proxing.
						// Otherwise there is a possibility that the backup is
						// in use by another proxing MAPE instance. 
						CheckPreviousBackup();

						RunProxyImpl(settings);
					} finally {
						lock (locker) {
							quitting = true;
							// The lock should be released after the event is set.
							// Otherwise, 'watch' therad does not exit if it is running just before forwardingEvent.Reset().
							forwardingEvent.Set();
						}
						watchingTask.Wait();
					}
				}
			}

			return;
		}

		protected void CheckPreviousBackup() {
			string backupFilePath = GetSystemSettingsBackupPath();
			if (File.Exists(backupFilePath)) {
				// backup file exists
				DateTime date = File.GetLastWriteTime(backupFilePath);
				string message = string.Format(Resources.CommandBase_Message_SystemSettingsAreNotRestored, date);
				bool? answer = Prompt(message, threeState: true);

				// process depending on answer
				//   true (Yes): restore the backup
				//   false (No): do not restore the backup, but delete it
				//   null (Cancel): do nothing
				if (answer.HasValue) {
					if (answer.Value) {
						// restore the backup
						JsonObjectData data = JsonObjectData.Load(backupFilePath, createIfNotExist: false);
						if (data != null) {
							SystemSettingsSwitcher switcher = this.ComponentFactory.CreateSystemSettingsSwitcher(this, null);
							switcher.Restore(data, systemSessionEnding: false);
						}
					}

					// delete backup file
					File.Delete(backupFilePath);
				}
			}

			return;
		}

		protected RunningProxyState StartProxy(CommandSettings settings, bool saveCredentials, bool checkPreviousBackup) {
			// argument checks
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}
			Debug.Assert(settings.SystemSettingsSwitcher != null);
			Debug.Assert(settings.Proxy != null);

			// create a RunningProxyState and start the proxy
			RunningProxyState state = new RunningProxyState(this);
			try {
				state.Start(settings, saveCredentials, checkPreviousBackup);
			} catch {
				state.Dispose();
				throw;
			}

			return state;
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

			// statistics of the ComponentFactory
			this.ComponentFactory.LogStatistics(recap: true);
		}

		public bool Test(CommandSettings settings, string targetUrl) {
			SystemSettingsSwitcherSettings systemSettingsSwitcherSettings = settings.SystemSettingsSwitcher;
			bool backup = systemSettingsSwitcherSettings.EnableSystemSettingsSwitch;
			string logLevelLog = string.Empty;
			TraceLevel backupLogLevel = Logger.LogLevel;
			TraceLevel tempLogLevel = backupLogLevel;
			if (tempLogLevel < TraceLevel.Info) {
				tempLogLevel = TraceLevel.Info;
				logLevelLog = $" The LogLevel is changed to '{tempLogLevel}' temporarily.";
			}

			// test suppressing system settings switch
			try {
				LogStart($"Start a test connection to '{targetUrl}'.{logLevelLog}");
				Logger.LogLevel = tempLogLevel;
				systemSettingsSwitcherSettings.EnableSystemSettingsSwitch = false;

				using (RunningProxyState proxyState = StartProxy(settings, saveCredentials: false, checkPreviousBackup: false)) {
                    // In SystemSettingsSwitcher.TestWebProxy() called from StartProxy() above,
                    // MAPE already tested connectivity to the targetUrl with the actual proxy.
                    // In this test, on the other hand, connectivity is tested with the MAPE main listener as the proxy.
                    IPEndPoint proxyEndPoint = ListenerSettings.GetEndPoint(settings.Proxy.MainListener);
					WebClientForTest webClient = new WebClientForTest();
                    webClient.Timeout = 180 * 1000; // 3 minutes
					webClient.Proxy = new WebProxy(proxyEndPoint.Address.ToString(), proxyEndPoint.Port);

					webClient.DownloadData(targetUrl);  // an exception is thrown on error

					// wait for stop for 3 seconds 
					proxyState.Stop(systemSessionEnding: false, millisecondsTimeout: 3000);
				}
			} finally {
				systemSettingsSwitcherSettings.EnableSystemSettingsSwitch = backup;
				Logger.LogLevel = backupLogLevel;
				LogStop($"Stop the test connection.");
			}

			return true;
		}

		public Task<bool> TestAsync(CommandSettings settings, string targetUrl) {
			return Task.Run<bool>(() => Test(settings, targetUrl));
		}

		#endregion


		#region methods - initial setup

		protected bool DoInitialSetup(CommandSettings settings) {
			// argument checks
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}
			int currentLevel = settings.InitialSetupLevel;

			// state checks
			string settingsFilePath = this.SettingsFilePath;
			if (string.IsNullOrEmpty(settingsFilePath)) {
				// it is temporary running
				return false;
			}

			// do initial setup if it has not been done
			bool result = false;
			if (currentLevel < SetupContext.LatestInitialSetupLevel) {
				int newLevel = DoInitialSetupImpl(settings);
				if (newLevel != currentLevel) {
					// settings are set up
					result = true;

					// set the 'InitialSetupDone' setting
					settings.InitialSetupLevel = newLevel;

					// save the new settings
					Action saveTask = () => {
						try {
							SaveSettingsToFile(settings, settingsFilePath);
						} catch (Exception exception) {
							LogError($"Fail to save settings: {exception.Message}");
						}
					};
					Task.Run(saveTask);
				}
			}

			return result;
		}

		#endregion


		#region methods - for RunningProxyState and SystemSettingSwitcher class

		public void ShowRestoreSystemSettingsErrorMessage(string message) {
			message = string.Format(
				Resources.RunningProxyState_Message_FailToRestoreSystemSettings,
				message ?? "(unknown reason)",
				GetSystemSettingsBackupPath()
			);
			ShowErrorMessage(message);
		}

		public void SaveSystemSettingsBackup(SystemSettings backup) {
			// argument checks
			if (backup == null) {
				throw new ArgumentNullException(nameof(backup));
			}

			// save the backup settings to the file
			string backupFilePath = GetSystemSettingsBackupPath();
			JsonObjectData data = JsonObjectData.CreateEmpty();
			backup.SaveToObjectData(data);
			data.Save(backupFilePath);

			return;
		}

		public void DeleteSystemSettingsBackup() {
			File.Delete(GetSystemSettingsBackupPath());
		}

		#endregion


		#region overridables - argument processing

		protected virtual CommandSettings ProcessArguments(string[] args) {
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

		protected virtual CommandSettings CreateExecutingSettings(IDictionary<string, string> options, IList<string> normalArguments) {
			// argument checks
			Debug.Assert(options != null);
			Debug.Assert(normalArguments != null);

			// create base settings
			IObjectData settingsData = GetBaseSettings(options);
			CommandSettings settings = this.ComponentFactory.CreateCommandSettings(settingsData);

			// load base settings
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

		protected virtual void LoadBaseSettings(CommandSettings settings) {
			// argument checks
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}

			// do nothing

			return;
		}

		protected virtual bool HandleOption(string name, string value, CommandSettings settings) {
			// argument checks
			Debug.Assert(name != null);
			// value may null
			Debug.Assert(settings != null);

			// handle option
			bool handled = true;
			if (AreSameOptionNames(name, OptionNames.Help) || AreSameOptionNames(name, "?")) {
				this.Kind = ExecutionKind.ShowUsage;
			} else if (AreSameOptionNames(name, OptionNames.SettingsFile) || AreSameOptionNames(name, OptionNames.NoSettings)) {
				// ignore, it was already handled in CreateSettings()
			} else if (AreSameOptionNames(name, OptionNames.LogLevel)) {
				settings.LogLevel = (TraceLevel)Enum.Parse(typeof(TraceLevel), value);
			} else if (AreSameOptionNames(name, OptionNames.Culture)) {
				settings.Culture = new CultureInfo(value);
			} else if (AreSameOptionNames(name, OptionNames.Credential)) {
				settings.AddCredential(new CredentialSettings(new JsonObjectData(value)));
			} else if (AreSameOptionNames(name, OptionNames.MainListener)) {
				settings.Proxy.MainListener = new ListenerSettings(new JsonObjectData(value));
			} else if (AreSameOptionNames(name, OptionNames.AdditionalListeners)) {
				settings.Proxy.AdditionalListeners = JsonObjectData.CreateArray(value).Select(v => new ListenerSettings(v.ExtractObjectValue())).ToArray();
			} else if (AreSameOptionNames(name, OptionNames.RetryCount)) {
				settings.Proxy.RetryCount = int.Parse(value);
			} else if (AreSameOptionNames(name, OptionNames.EnableSystemSettingsSwitch)) {
				settings.SystemSettingsSwitcher.EnableSystemSettingsSwitch = bool.Parse(value);
			} else if (AreSameOptionNames(name, OptionNames.ActualProxy)) {
				settings.SystemSettingsSwitcher.ActualProxy = new ActualProxySettings(new JsonObjectData(value));
			} else {
				handled = false;	// not handled
			}

			return handled;
		}

		protected virtual bool HandleArgument(string arg, CommandSettings settings) {
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
				CommandSettings settings = null;
				try {
					settings = ProcessArguments(args);
					Debug.Assert(settings != null);
				} catch (Exception exception) {
					// show usage
					ShowErrorMessage(exception.Message);
					this.Kind = ExecutionKind.ShowUsage;
				}

				// common settings
				// Note that settings may be null in an error case.
				if (settings != null) {
					// set culture
					CultureInfo culture = settings.Culture;
					if (culture != null) {
						Thread.CurrentThread.CurrentCulture = culture;
						Thread.CurrentThread.CurrentUICulture = culture;
					}

					// set log level
					Logger.LogLevel = settings.LogLevel;
				}

				// execute command based on the settings
				Execute(this.Kind, settings);
			} catch (Exception exception) {
				ShowErrorMessage(exception.Message);
			} finally {
				Logger.StopLogging(1000);
			}

			return;
		}

		public virtual void Execute(string commandKind, CommandSettings settings) {
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

		protected abstract void ShowUsage(CommandSettings settings);

		protected abstract void RunProxyImpl(CommandSettings settings);

		protected virtual CredentialSettings UpdateCredential(string endPoint, string realm, CredentialSettings oldCredential) {
			return null;	// no credential by default
		}

		#endregion


		#region overridables - misc

		protected abstract void ShowErrorMessage(string message);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="message"></param>
		/// <returns>
		///   <list type="bullet">
		///     <item>true: Yes</item>
		///     <item>false: No</item>
		///     <item>null: Cancel</item>
		///   </list>
		/// </returns>
		protected abstract bool? Prompt(string message, bool threeState);

		protected virtual void BringAppToForeground() {
		}

		protected virtual int DoInitialSetupImpl(CommandSettings settings) {
			return SetupContext.LatestInitialSetupLevel;	// assumes set up
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

		private IObjectData GetBaseSettings(IDictionary<string, string> options) {
			// argument checks
			Debug.Assert(options != null);

			// create base settings
			IObjectData settingsData = null;
			bool noSetting = options.ContainsKey(OptionNames.NoSettings);
			if (noSetting == false) {
				// load the settings

				// find the settings file path 
				string settingsFilePath;
				if (options.TryGetValue(OptionNames.SettingsFile, out settingsFilePath) == false) {
					// default location is %LOCALAPPDATA%\MAPE
					settingsFilePath = Path.Combine(GetMAPEAppDataFolder(), "Settings.json");
				}

				// load settings from the config file
				try {
					settingsData = LoadSettingsFromFile(true, settingsFilePath);
					this.SettingsFilePath = settingsFilePath;
				} catch (Exception exception) {
					string message = string.Format(Resources.CommandBase_Message_FailToLoadSettingsFile, settingsFilePath, exception.Message);
					ShowErrorMessage(message);
				}
			}
			if (settingsData == null) {
				settingsData = JsonObjectData.CreateEmpty();
			}

			return settingsData;
		}

		private static string GetMAPEAppDataFolder() {
			// default folder is %LOCALAPPDATA%\MAPE
			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"MAPE"
			);
		}

		private static string GetSystemSettingsBackupPath() {
			return Path.Combine(GetMAPEAppDataFolder(), "SystemSettingsBackup.json");
		}

		private static string GetForwardingEventName() {
			return $"MAPE_{Environment.UserDomainName}_{Environment.UserName}";
		}

		#endregion
	}
}
