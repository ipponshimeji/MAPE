using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using MAPE.Utils;
using MAPE.Properties;


namespace MAPE.Command {
    public abstract class CLICommandBase: CommandBase {
		#region types

		public static new class OptionNames {
			#region constants

			public const string Save = "Save";

			public const string NoLogo = SettingNames.NoLogo;

			#endregion
		}

		public static new class SettingNames {
			#region constants

			public const string NoLogo = "NoLogo";

			#endregion
		}

		public new class ExecutionKind: CommandBase.ExecutionKind {
			#region constants

			public const string SaveSettings = "SaveSettings";

			#endregion
		}

		protected enum ControllerThreadEventKind {
			None = 0,
			Quit = 1,
			Suspend = 2,
			Resume = 3,
		}

		#endregion


		#region data - data synchronized by controllerThreadEventLocker

		private object controllerThreadEventLocker = new object();

		private ManualResetEvent controllerThreadEvent = null;

		private ControllerThreadEventKind controllerThreadEventKind = ControllerThreadEventKind.None;

		#endregion


		#region properties

		private ManualResetEvent ControllerThreadEvent {
			get {
				return this.controllerThreadEvent;
			}
			set {
				lock (this.controllerThreadEventLocker) {
					this.controllerThreadEvent = value;
				}
			}
		}

		#endregion


		#region creation and disposal

		public CLICommandBase(ComponentFactory componentFactory): base(componentFactory) {
		}

		#endregion


		#region methods

		protected void OutputStandardLogo(Assembly assembly) {
			Console.WriteLine(Resources.CLICommandBase_Logo_Command);
			if (assembly != null) {
				string version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
				if (string.IsNullOrEmpty(version) == false) {
					Console.WriteLine("version " + version);
				}
				string copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
				if (string.IsNullOrEmpty(copyright) == false) {
					Console.WriteLine(copyright);
				}
			}
			Console.WriteLine();

			return;
		}

		protected void AwakeControllerThread(ControllerThreadEventKind kind) {
			lock (this.controllerThreadEventLocker) {
				ManualResetEvent controllerThreadEvent = this.ControllerThreadEvent;
				if (controllerThreadEvent != null) {
					this.controllerThreadEventKind = kind;
					controllerThreadEvent.Set();
				}
			}

			return;
		}

		#endregion


		#region overrides/overridables - argument processing

		protected override bool HandleOption(string name, string value, SettingsData settings) {
			// handle option
			bool handled = true;
			if (AreSameOptionNames(name, OptionNames.Save)) {
				this.Kind = ExecutionKind.SaveSettings;
			} else if (AreSameOptionNames(name, OptionNames.NoLogo)) {
				settings.SetBooleanValue(SettingNames.NoLogo, true);
			} else {
				handled = base.HandleOption(name, value, settings);
			}

			return handled;
		}

		#endregion


		#region overrides/overridables - execution

		public override void Run(string[] args) {
			// connect a ColorConsoleTraceListener during its execution to show color-coded log in the console
			ColorConsoleLogMonitor monitor = new ColorConsoleLogMonitor();
			Logger.AddLogMonitor(monitor);
			try {
				base.Run(args);
			} finally {
				Logger.RemoveLogMonitor(monitor);
			}
		}

		public override void Execute(string commandKind, SettingsData settings) {
			// argument checks
			Debug.Assert(commandKind != null);

			// show logo
			if (settings.GetBooleanValue(SettingNames.NoLogo, false) == false) {
				OutputLogo();
			}

			// execute command according to the command kind 
			switch (commandKind) {
				case ExecutionKind.SaveSettings:
					SaveSettings(settings);
					break;
				default:
					base.Execute(commandKind, settings);
					break;
			}

			return;
		}

		protected override void RunProxy(SettingsData settings) {
			// argument checks
			Debug.Assert(settings.IsNull == false);

			using (ManualResetEvent controllerThreadEvent = new ManualResetEvent(false)) {
				this.ControllerThreadEvent = controllerThreadEvent;
				try {
					// prepare Ctrl+C handler
					ConsoleCancelEventHandler ctrlCHandler = (o, e) => {
						e.Cancel = true;
						AwakeControllerThread(ControllerThreadEventKind.Quit);
					};

					// set up Ctrl+C handler 
					Console.CancelKeyPress += ctrlCHandler;

					ControllerThreadEventKind eventKind = ControllerThreadEventKind.None;
					do {
						// run the proxy
						bool completed = false;
						using (RunningProxyState runningProxyState = StartProxy(settings, this)) {
							// log & message
							LogProxyStarted(eventKind == ControllerThreadEventKind.Resume);
							Console.WriteLine(Resources.CLICommandBase_Message_StartListening);
							Console.WriteLine(Resources.CLICommandBase_Message_StartingNote);

							// wait for Ctrl+C or other events
							controllerThreadEvent.WaitOne();
							eventKind = this.controllerThreadEventKind;

							// stop the proxy
							completed = runningProxyState.Stop(5000);
						}
						LogProxyStopped(completed, eventKind == ControllerThreadEventKind.Suspend);
						Console.WriteLine(completed ? Resources.CLICommandBase_Message_Completed : Resources.CLICommandBase_Message_NotCompleted);

						// process the event which awake this thread
						while (eventKind == ControllerThreadEventKind.Suspend) {
							// wait for the next event
							controllerThreadEvent.Reset();
							controllerThreadEvent.WaitOne();
							eventKind = this.controllerThreadEventKind;
						}
						if (eventKind != ControllerThreadEventKind.Resume) {
							// quit
							Debug.Assert(eventKind == ControllerThreadEventKind.Quit);
							break;
						}
						controllerThreadEvent.Reset();
					} while (true);

					// clean up Ctrl+C handler
					Console.CancelKeyPress -= ctrlCHandler;
				} finally {
					this.ControllerThreadEvent = null;
				}
			}

			return;
		}

		protected override CredentialInfo UpdateCredential(string endPoint, string realm, CredentialInfo oldCredential) {
			// argument checks
			Debug.Assert(endPoint != null);
			Debug.Assert(realm != null);    // may be empty

			return AskCredentialInfo(endPoint, realm, canSave: this.HasSettingsFile);
		}

		protected virtual void SaveSettings(SettingsData settings) {
			// argument checks
			Debug.Assert(settings.IsNull == false);

			// state checks
			if (this.HasSettingsFile == false) {
				throw new Exception(Resources.CLICommandBase_Message_NoSettingsFile);
			}

			// save the settings
			SaveSettingsToFile(settings);

			string message = string.Format(Resources.CLICommandBase_SaveSettings_Completed, this.SettingsFilePath);
			Console.WriteLine(message);
		}

		protected virtual void OutputLogo() {
			OutputStandardLogo(null);
		}

		#endregion


		#region overrides/overridables - misc

		protected override void ShowErrorMessage(string message) {
			Console.Error.WriteLine(message);
		}

		#endregion


		#region privates

		private static CredentialInfo AskCredentialInfo(string endPoint, string realm, bool canSave) {
			// argument checks
			Debug.Assert(realm != null);

			// read information from the console
			Console.WriteLine(Resources.CLICommandBase_AskCredential_Description, endPoint);
			Console.WriteLine($"Realm: {realm}");
			Console.Write(Resources.CLICommandBase_AskCredential_UserName);
			string userName = Console.ReadLine();
			Console.Write(Resources.CLICommandBase_AskCredential_Password);
			string password = ReadPassword();
			CredentialPersistence persistence = AskCredentialPersistence(canSave);
			bool enableAssumptionMode = AskEnableAssumptionMode();

			return new CredentialInfo(endPoint, userName, password, persistence, enableAssumptionMode);
		}

		private static CredentialPersistence AskCredentialPersistence(bool canSave) {
			// read user preference from the console
			do {
				Console.WriteLine(Resources.CLICommandBase_AskCredential_Persistence_Description);
				Console.WriteLine($"  1: {Resources.CLICommandBase_AskCredential_Persistence_Session}");
				Console.WriteLine($"  2: {Resources.CLICommandBase_AskCredential_Persistence_Process}");
				if (canSave) {
					Console.WriteLine($"  3: {Resources.CLICommandBase_AskCredential_Persistence_Persistent}");
				}
				Console.Write(Resources.CLICommandBase_AskCredential_Persistence_Prompt);
				string answer = Console.ReadLine();

				int number;
				if (int.TryParse(answer, out number)) {
					switch (number) {
						case 1:
							return CredentialPersistence.Session;
						case 2:
							return CredentialPersistence.Process;
						case 3:
							if (canSave) {
								return CredentialPersistence.Persistent;
							}
							break;
					}
				}
			} while (true);
		}

		private static bool AskEnableAssumptionMode() {
			// read user preference from the console
			do {
				Console.WriteLine(Resources.CLICommandBase_AskCredential_EnableAssumptionMode_Description);
				Console.Write(Resources.CLICommandBase_AskCredential_EnableAssumptionMode_Prompt);
				string answer = Console.ReadLine().Trim();

				switch (answer) {
					case "":
					case "Y":
					case "y":
						return true;
					case "N":
					case "n":
						return false;
				}
			} while (true);
		}

		// thanks to http://stackoverflow.com/questions/3404421/password-masking-console-application
		private static string ReadPassword() {
			var buf = new StringBuilder();
			do {
				var keyInfo = Console.ReadKey(intercept: true);
				switch (keyInfo.Key) {
					case ConsoleKey.Enter:
						Console.WriteLine();
						return buf.ToString();
					case ConsoleKey.Backspace:
						if (0 < buf.Length) {
							buf = buf.Remove(buf.Length - 1, 1);
							Console.Write("\b \b");
						}
						break;
					default:
						buf.Append(keyInfo.KeyChar);
						Console.Write("*");
						break;
				}
			} while (true);
		}

		#endregion
	}
}
