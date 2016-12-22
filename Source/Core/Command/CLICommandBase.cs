using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using MAPE.Utils;
using MAPE.Configuration;
using MAPE.Server;


namespace MAPE.Command {
    public class CLICommandBase: CommandBase {
		#region types

		public static new class OptionNames {
			#region constants

			public const string Save = "Save";

			#endregion
		}

		public new class CommandKind: CommandBase.CommandKind {
			#region constants

			public const string SaveConfiguration = "SaveConfiguration";

			#endregion
		}

		#endregion


		#region data
		#endregion


		#region creation and disposal

		public CLICommandBase(ComponentFactory componentFactory): base(componentFactory) {
		}

		#endregion


		#region methods
		#endregion


		#region overrides/overridables - argument processing

		protected override bool HandleOption(Parameter option) {
			// handle option
			bool handled = true;
			if (option.IsName(OptionNames.Save)) {
				this.Kind = CommandKind.SaveConfiguration;
			} else {
				handled = base.HandleOption(option);
			}

			return handled;
		}

		#endregion


		#region overrides/overridables - execution

		public override void Run(string[] args) {
			ColorCodedConsoleTraceListener traceListener = new ColorCodedConsoleTraceListener(true);
			Trace.Listeners.Add(traceListener);
			try {
				base.Run(args);
			} finally {
				Trace.Listeners.Remove(traceListener);
			}
		}

		public override void Execute(ProxyConfiguration proxyConfiguration) {
			// argument checks
			Debug.Assert(proxyConfiguration != null);

			// execute command according to the command kind 
			switch (this.Kind) {
				case CommandKind.SaveConfiguration:
					// adjust proxyConfiguration
					// Generally, the password given from command line is supposed to be volatile.
					// But in the SaveConfiguration mode, the password should be saved.
					proxyConfiguration.ProxyCredentialPersistence = CredentialPersistence.Persistent;
					SaveConfiguration(proxyConfiguration);
					break;
				default:
					base.Execute(proxyConfiguration);
					break;
			}

			return;
		}

		protected virtual void SaveConfiguration(ProxyConfiguration proxyConfiguration) {
			// argument checks
			Debug.Assert(proxyConfiguration != null);

			// save the configuration
			proxyConfiguration.SaveConfiguration();
			// ToDo: saved message
		}

		protected override void RunProxy(ProxyConfiguration proxyConfiguration) {
			// argument checks
			Debug.Assert(proxyConfiguration != null);

			// run proxy server
			using (Proxy proxy = this.ComponentFactory.CreateProxy(proxyConfiguration)) {
				// start the proxy
				proxy.CredentialCallback = GetCredential;
				proxy.Start();
				Console.WriteLine("Listening...");
				Console.WriteLine("Press Ctrl+C to quit.");

				// wait for Ctrl+C
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
				bool completed = proxy.Stop(5000);
				Console.WriteLine(completed? "Completed.": "Not Completed.");	// ToDo: message
			}
		}

		#endregion


		#region privates

		private static NetworkCredential GetCredential(string realm) {
			// read information from the console
			Console.WriteLine($"Credential for {realm} is required.");
			Console.Write("UserName: ");
			string userName = Console.ReadLine();
			Console.Write("Password: ");
			string password = ReadPassword();

			return new NetworkCredential(userName, password);
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
