using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using MAPE.Utils;
using MAPE.Server;


namespace MAPE.Command {
    public class CLICommandBase: CommandBase, IProxyRunner {
		#region types

		public static new class OptionNames {
			#region constants

			public const string Save = "Save";

			#endregion
		}

		public new class CommandKind: CommandBase.CommandKind {
			#region constants

			public const string SaveSettings = "SaveSettings";

			#endregion
		}

		#endregion


		#region data
		#endregion


		#region creation and disposal

		public CLICommandBase(ComponentFactory componentFactory): base(componentFactory) {
		}

		#endregion


		#region IProxyRunner

		public NetworkCredential AskCredential(Proxy proxy, string realm, bool needUpdate) {
			NetworkCredential credential = this.Credential;
			if (needUpdate || credential == null) {
				// read information from the console
				Console.WriteLine($"Credential for {realm} is required.");
				Console.Write("UserName: ");
				string userName = Console.ReadLine();
				Console.Write("Password: ");
				string password = ReadPassword();

				CredentialPersistence credentialPersistence;
				do {
					Console.WriteLine($"How save password?");
					Console.WriteLine($"  1: only during this http session");
					Console.WriteLine($"  2: during running this process");
					Console.WriteLine($"  3: save the password in settings file");
					Console.Write("No: ");
					string line = Console.ReadLine();

					int number;
					if (int.TryParse(line, out number)) {
						if (number == 1) {
							credentialPersistence = CredentialPersistence.Session;
							break;
						} else if (number == 2) {
							credentialPersistence = CredentialPersistence.Process;
							break;
						} else if (number == 3) {
							credentialPersistence = CredentialPersistence.Persistent;
							break;
						}
					}
				} while (true);

				credential = new NetworkCredential(userName, password);
				SetCredential(credentialPersistence, credential, saveIfNecessary: true);
				proxy.IsServerCredentialPersistencyProcess = (credentialPersistence != CredentialPersistence.Session);
			}

			// return the clone of this.Credential
			return new NetworkCredential(credential.UserName, credential.Password);
		}

		#endregion


		#region overrides/overridables - argument processing

		protected override bool HandleOption(string name, string value, Settings settings) {
			// handle option
			bool handled = true;
			if (AreSameOptionNames(name, OptionNames.Save)) {
				this.Kind = CommandKind.SaveSettings;
			} else {
				handled = base.HandleOption(name, value, settings);
			}

			return handled;
		}

		#endregion


		#region overrides/overridables - execution

		public override void Run(string[] args) {
			// connect a ColorConsoleTraceListener during its execution to show color-coded log in the console
			ColorConsoleTraceListener traceListener = new ColorConsoleTraceListener(true);
			Logger.Source.Listeners.Add(traceListener);
			try {
				base.Run(args);
			} finally {
				Logger.Source.Listeners.Remove(traceListener);
			}
		}

		public override void Execute(string commandKind, Settings settings) {
			// argument checks
			Debug.Assert(settings.IsNull == false);

			// execute command according to the command kind 
			switch (commandKind) {
				case CommandKind.SaveSettings:
					SaveSettings(settings);
					break;
				default:
					base.Execute(commandKind, settings);
					break;
			}

			return;
		}

		protected override void RunProxy(Settings settings) {
			// argument checks
			Debug.Assert(settings.IsNull == false);

			// run the proxy
			bool completed = false;
			using (RunningProxyState runningProxyState = StartProxy(settings, this)) {
				// wait for Ctrl+C
				Console.WriteLine("Listening...");
				Console.WriteLine("Press Ctrl+C to quit.");
				using (ManualResetEvent quitEvent = new ManualResetEvent(false)) {
					// setup Ctrl+C handler
					ConsoleCancelEventHandler ctrlCHandler = (o, e) => {
						e.Cancel = true;
						quitEvent.Set();
					};
					Console.CancelKeyPress += ctrlCHandler;

					// wait for Ctrl+C
					quitEvent.WaitOne();

					// cleanup Ctrl+C handler
					Console.CancelKeyPress -= ctrlCHandler;
				}

				// stop the proxy
				completed = runningProxyState.Stop(5000);
			}
			Console.WriteLine(completed ? "Completed." : "Not Completed."); // ToDo: message

			return;
		}

		protected virtual void SaveSettings(Settings settings) {
			// argument checks
			Debug.Assert(settings.IsNull == false);

			// adjust the settings
			// The CredentialPersistence is supposed to be 'Persist'
			// if /UserName or /Password option is specified from command line with /Save option
			string userName = settings.GetStringValue(SettingNames.UserName, defaultValue: null);
			string protectedPasswoed = settings.GetStringValue(SettingNames.ProtectedPassword, defaultValue: null);
			if (userName != null || protectedPasswoed != null) {
				settings.SetStringValue(SettingNames.CredentialPersistence, CredentialPersistence.Persistent.ToString());
			}

			// save the settings
			SaveSettingsToFile(settings);
		}

		#endregion


		#region privates

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
