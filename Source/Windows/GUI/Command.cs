using System;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using MAPE.Command;
using MAPE.Command.Settings;
using MAPE.Windows.GUI.Settings;


namespace MAPE.Windows.GUI {
	internal class Command: GUICommandBase {
		#region data

		private App app = null;

		#endregion


		#region properties

		public new CommandForWindowsGUISettings Settings {
			get {
				return (CommandForWindowsGUISettings)base.Settings;
			}
			set {
				base.Settings = value;
			}
		}

		public GUIForWindowsGUISettings GUISettings {
			get {
				return this.Settings.GUI;
			}
		}

		#endregion


		#region creation and disposal

		public Command(): base(new ComponentFactoryForWindowsGUI()) {
			// initialize members
			this.ComponentName = "MAPE GUI";

			return;
		}

		#endregion


		#region methods

		public void DoInitialSetup() {
			base.DoInitialSetup(this.Settings);
		}

		public void SetSettings(CommandForWindowsGUISettings newSettings, bool save) {
			// change the current settings
			CommandForWindowsGUISettings oldSettings = this.Settings;
			this.Settings = newSettings;
			OnSettingsChanged(newSettings, oldSettings);

			// save the settings if necessary
			if (save) {
				string settingsFilePath = this.SettingsFilePath;
				if (string.IsNullOrEmpty(settingsFilePath) == false) {
					Action saveTask = () => {
						try {
							SaveSettingsToFile(newSettings, settingsFilePath);
						} catch (Exception exception) {
							LogError($"Fail to save settings: {exception.Message}");
						}
					};

					// launch save task
					Task.Run(saveTask);
				}
			}
		}

		public void SaveMainWindowSettings(MainWindowSettings mainWindowSettings) {
			string settingsFilePath = this.SettingsFilePath;
			if (string.IsNullOrEmpty(settingsFilePath) == false) {
				MainWindowSettings mainWindowSettingsClone = CloneSettings(mainWindowSettings);
				Action saveTask = () => {
					try {
						UpdateSettingsFile(s => { ((GUIForWindowsGUISettings)s.GUI).MainWindow = mainWindowSettingsClone; });
					} catch (Exception exception) {
						LogError($"Fail to save MainWindow settings: {exception.Message}");
					}
				};

				// launch save task
				Task.Run(saveTask);
			} else {
				LogError($"Fail to save MainWindow settings: no settings file is used.");
			}
		}

		#endregion


		#region overrides/overridables - execution

		protected override void ShowUsage(CommandSettings settings) {
			// show the Usage page in the browser
			Process.Start(GetUsagePagePath());
		}

		protected override void RunProxyImpl(CommandSettings settings) {
			// state checks
			if (this.app != null) {
				throw new InvalidOperationException();
			}

			SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
			try {
				// run WPF application
				App app = new App(this);
				app.InitializeComponent();
				this.app = app;
				try {
					// start application
					app.Run();
				} finally {
					this.app = null;
				}
			} finally {
				SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
			}

			return;
		}

		protected override CredentialSettings UpdateCredential(string endPoint, string realm, CredentialSettings oldCredential) {
			// argument checks
			Debug.Assert(endPoint != null);
			Debug.Assert(realm != null);    // may be empty
			// oldCredential can be null

			// state checks
			Debug.Assert(this.app != null);

			// clone the CredentialSettings
			CredentialSettings credentialSettings;
			if (oldCredential != null) {
				credentialSettings = CredentialSettings.Clone(oldCredential);
			} else {
				credentialSettings = new CredentialSettings();
				// set default Persistence value to Session (i.e. volatile)
				credentialSettings.Persistence = CredentialPersistence.Session;
			}
			credentialSettings.EndPoint = endPoint;

			// ask user's credential
			Func<CredentialSettings> callback = () => {
				// get the MainWindow
				// Note that the MainWindow can be accessed only from the GUI thread
				// (that is, it must be gotten inside this callback)
				Window mainWindow = this.app.MainWindow;

				// prepare CredentialDialog
				CredentialDialog dialog = new CredentialDialog(credentialSettings);
				dialog.Title = realm;
				if (mainWindow != null) {
					dialog.Owner = mainWindow;
				}

				// show the credential dialog and get user input
				return (dialog.ShowDialog() ?? false) ? dialog.CredentialSettings : null;
			};

			return this.app.Dispatcher.Invoke<CredentialSettings>(callback);
		}

		#endregion


		#region overrides/overridables - misc

		protected override void ShowErrorMessage(string message) {
			Action showErrorMessage = () => {
				MessageBox.Show(message, this.ComponentName, MessageBoxButton.OK, MessageBoxImage.Error);
			};

			App app = this.app;
			if (app == null) {
				showErrorMessage();
			} else {
				app.Dispatcher.Invoke(showErrorMessage);
			}

			return;
		}

		protected override bool? Prompt(string message, bool threeState) {
			MessageBoxButton button = threeState ? MessageBoxButton.YesNoCancel : MessageBoxButton.YesNo;
			Func<bool?> prompt = () => {
				switch (MessageBox.Show(message, this.ComponentName, button, MessageBoxImage.None)) {
					case MessageBoxResult.Yes:
						return true;
					case MessageBoxResult.No:
						return false;
					case MessageBoxResult.Cancel:
					default:
						return null;
				}
			};

			return (this.app == null)? prompt(): this.app.Dispatcher.Invoke<bool?>(prompt);
		}

		protected override void BringAppToForeground() {
			this.app.Dispatcher.Invoke(
				() => { this.app.OpenMainWindow(); }
			);
		}

		#endregion


		#region privates

		private static string GetUsagePagePath() {
			// detect the folder path where the usage page is located
			// (that is the application folder)
			string folderPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

			// find the usage page for the current locale
			CultureInfo culture = CultureInfo.CurrentUICulture;
			while (string.IsNullOrEmpty(culture.Name) == false) {
				string filePath = Path.Combine(folderPath, $"Usage.{culture.Name}.html");
				if (string.Compare(culture.Name, "ja", StringComparison.OrdinalIgnoreCase) == 0) {
					return "https://github.com/ipponshimeji/MAPE/blob/master/Documentation/ja/Usage.md";
				}

				culture = culture.Parent;
			}

			// ToDo: English Pages
			return "https://github.com/ipponshimeji/MAPE";
		}

		#endregion


		#region event handler

		private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e) {
			try {
				switch (e.Mode) {
					case PowerModes.Suspend:
						SuspendProxy();
						break;
					case PowerModes.Resume:
						ResumeProxy();
						break;
				}
			} catch (Exception exception) {
				LogError($"Fail to {e.Mode}: {exception.Message}");
				// continue
			}
		}

		#endregion


		#region entry point

		[STAThread]
		static void Main(string[] args) {
			using (Command command = new Command()) {
				command.Run(args);
			}

			return;
		}

		#endregion
	}
}
