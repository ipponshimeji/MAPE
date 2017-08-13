using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using MAPE.Utils;
using MAPE.Properties;
using MAPE.Command.Settings;


namespace MAPE.Command {
    public abstract class CLICommandBase: CommandBase {
		#region types

		public static new class OptionNames {
			#region constants

			public const string Save = "Save";

			public const string NoLogo = CommandSettings.SettingNames.NoLogo;

			#endregion
		}

		public new class ExecutionKind: CommandBase.ExecutionKind {
			#region constants

			public const string SaveSettings = "SaveSettings";

			#endregion
		}

		protected class ControllerThreadSynchronizer: IDisposable {
			#region types

			public enum EventKind {
				None = 0,
				Quit = 1,					// Ctrl+C
				SystemSessionEnding = 2,	// logging off or shutting down
				Suspend = 3,
				Resume = 4,
			}

			#endregion


			#region data

			private AutoResetEvent syncEvent;

			public EventKind Event { get; private set; }

			#endregion


			#region creation and disposal

			public ControllerThreadSynchronizer() {
				// initialize members
				this.syncEvent = new AutoResetEvent(false);
				this.Event = EventKind.None;

				return;
			}

			public void Dispose() {
				// dispose members
				DisposableUtil.ClearDisposableObject(ref this.syncEvent);

				return;
			}

			#endregion


			#region methods - called from the controller thread

			public EventKind WaitForEvent() {
				// state checks
				EventWaitHandle syncEvent = GetSyncEventOrThrowDisposedException();

				// wait for the event
				this.Event = EventKind.None;
				syncEvent.WaitOne();

				return this.Event;  // Event was set by NotifyEventAndWaitForEventHandling() call
			}

			public void NotifyEventHandledAndWaitForAcknowledgment() {
				// state checks
				EventWaitHandle syncEvent = GetSyncEventOrThrowDisposedException();

				// notify the completion of the event handling
				syncEvent.Set();

				// wait for the acknowledgment from the waiting thread
				// Do not dispose this until the acknowledgment is returned.
				syncEvent.WaitOne();

				return;
			}

			#endregion


			#region methods - called from other threads than the controller thread

			public void NotifyEventAndWaitForEventHandling(EventKind eventKind) {
				// argument checks
				if (eventKind == EventKind.None) {
					throw new ArgumentException($"It must not be {nameof(EventKind.None)}", nameof(eventKind));
				}

				// state checks
				Debug.Assert(this.Event == EventKind.None);
				EventWaitHandle syncEvent = GetSyncEventOrThrowDisposedException();

				// notify the event
				this.Event = eventKind;
				syncEvent.Set();

				// wait for completion of the event handling
				syncEvent.WaitOne();

				// notify the acknowledgment
				syncEvent.Set();
				syncEvent = null;    // syncEvent may be disposed after the acknowledgment

				return;
			}

			#endregion


			#region privates

			private AutoResetEvent GetSyncEventOrThrowDisposedException() {
				// state checks
				AutoResetEvent value = this.syncEvent;
				if (value == null) {
					throw new ObjectDisposedException(nameof(ControllerThreadSynchronizer));
				}

				return value;
			}

			#endregion
		}

		#endregion


		#region data - data synchronized by controllerThreadSynchronizationLocker

		private object controllerThreadSynchronizationLocker = new object();

		private ControllerThreadSynchronizer controllerThreadSynchronizer = null;

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

		protected void RegisterControllerThreadSynchronizer(ControllerThreadSynchronizer synchronizer) {
			lock (this.controllerThreadSynchronizationLocker) {
				// state checks
				if (this.controllerThreadSynchronizer != null) {
					// another synchronizer is already registered
					throw new InvalidOperationException();
				}

				// register the synchronizer
				this.controllerThreadSynchronizer = synchronizer;
			}

			return;
		}

		protected void AwakeControllerThread(ControllerThreadSynchronizer.EventKind eventKind) {
			// try to acquire the synchronizer
			ControllerThreadSynchronizer synchronizer;
			lock (this.controllerThreadSynchronizationLocker) {
				synchronizer = this.controllerThreadSynchronizer;
				this.controllerThreadSynchronizer = null;
			}

			// notify the event if the synchronizer is acquired
			if (synchronizer != null) {
				synchronizer.NotifyEventAndWaitForEventHandling(eventKind);
			}

			return;
		}

		#endregion


		#region overrides/overridables - argument processing

		protected override bool HandleOption(string name, string value, CommandSettings settings) {
			// handle option
			bool handled = true;
			if (AreSameOptionNames(name, OptionNames.Save)) {
				this.Kind = ExecutionKind.SaveSettings;
			} else if (AreSameOptionNames(name, OptionNames.NoLogo)) {
				settings.NoLogo = bool.Parse(value);
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

		public override void Execute(string commandKind, CommandSettings settings) {
			// argument checks
			Debug.Assert(commandKind != null);

			// show logo
			if (settings.NoLogo == false) {
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

		protected override void RunProxyImpl(CommandSettings settings) {
			// argument checks
			Debug.Assert(settings != null);

			using (ControllerThreadSynchronizer synchronizer = new ControllerThreadSynchronizer()) {
				// prepare Ctrl+C handler
				ConsoleCancelEventHandler onCtrlC = (o, e) => {
					e.Cancel = true;
					AwakeControllerThread(ControllerThreadSynchronizer.EventKind.Quit);
				};

				// connect Ctrl+C handler 
				Console.CancelKeyPress += onCtrlC;
				try {
					ControllerThreadSynchronizer.EventKind eventKind = ControllerThreadSynchronizer.EventKind.None;
					do {
						Debug.Assert(eventKind == ControllerThreadSynchronizer.EventKind.None || eventKind == ControllerThreadSynchronizer.EventKind.Resume);

						// run the proxy
						bool completed = false;
						using (RunningProxyState runningProxyState = StartProxy(settings, saveCredentials: true, checkPreviousBackup: false)) {
							// log & message
							LogProxyStarted(eventKind == ControllerThreadSynchronizer.EventKind.Resume);
							Console.WriteLine(Resources.CLICommandBase_Message_StartListening);
							Console.WriteLine(Resources.CLICommandBase_Message_StartingNote);

							// wait for Ctrl+C or other events
							RegisterControllerThreadSynchronizer(synchronizer);
							eventKind = synchronizer.WaitForEvent();

							// stop the proxy
							completed = runningProxyState.Stop(eventKind == ControllerThreadSynchronizer.EventKind.SystemSessionEnding, 5000);
						}
						LogProxyStopped(completed, eventKind == ControllerThreadSynchronizer.EventKind.Suspend);
						Console.WriteLine(completed ? Resources.CLICommandBase_Message_Completed : Resources.CLICommandBase_Message_NotCompleted);

						// resume the thread which originally accepts the event
						synchronizer.NotifyEventHandledAndWaitForAcknowledgment();

						// decide the next step
						while (eventKind == ControllerThreadSynchronizer.EventKind.Suspend) {
							// wait for the next event
							RegisterControllerThreadSynchronizer(synchronizer);
							eventKind = synchronizer.WaitForEvent();
							synchronizer.NotifyEventHandledAndWaitForAcknowledgment();
						}
						if (eventKind != ControllerThreadSynchronizer.EventKind.Resume) {
							// quit
							Debug.Assert(eventKind == ControllerThreadSynchronizer.EventKind.Quit || eventKind == ControllerThreadSynchronizer.EventKind.SystemSessionEnding);
							break;
						}
					} while (true);
				} finally {
					// disconnect Ctrl+C handler
					Console.CancelKeyPress -= onCtrlC;
				}
			}

			return;
		}

		protected override CredentialSettings UpdateCredential(string endPoint, string realm, CredentialSettings oldCredential) {
			// argument checks
			Debug.Assert(endPoint != null);
			Debug.Assert(realm != null);    // may be empty

			return AskCredentialInfo(endPoint, realm, canSave: this.HasSettingsFile);
		}

		protected virtual void SaveSettings(CommandSettings settings) {
			// argument checks
			Debug.Assert(settings != null);

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

		protected override bool? Prompt(string message, bool threeState) {
			string promptMessage = threeState ? Resources.CLICommandBase_Prompt_YesNoCancel : Resources.CLICommandBase_Prompt_YesNo;

			// read user preference from the console
			Console.WriteLine(message);
			do {
				Console.Write(promptMessage);
				string answer = Console.ReadLine().Trim();

				switch (answer) {
					case "Y":
					case "y":
						return true;
					case "N":
					case "n":
						return false;
					case "C":
					case "c":
						if (threeState) {
							return null;
						}
						break;	// continue
				}
			} while (true);
		}

		#endregion


		#region privates

		private static CredentialSettings AskCredentialInfo(string endPoint, string realm, bool canSave) {
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

			return new CredentialSettings(endPoint, userName, password, persistence, enableAssumptionMode);
		}

		private static CredentialPersistence AskCredentialPersistence(bool canSave) {
			// read user preference from the console
			Console.WriteLine(Resources.CLICommandBase_AskCredential_Persistence_Description);
			Console.WriteLine($"  1: {Resources.CLICommandBase_AskCredential_Persistence_Session}");
			Console.WriteLine($"  2: {Resources.CLICommandBase_AskCredential_Persistence_Process}");
			if (canSave) {
				Console.WriteLine($"  3: {Resources.CLICommandBase_AskCredential_Persistence_Persistent}");
			}
			do {
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
			// ToDo: use Prompt()?
			// read user preference from the console
			Console.WriteLine(Resources.CLICommandBase_AskCredential_EnableAssumptionMode_Description);
			do {
				Console.Write(Resources.CLICommandBase_Prompt_YesNo);
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
