using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;


namespace MAPE.Core {
    public class CommandBase: IDisposable {
		#region data
		#endregion


		#region creation and disposal

		public CommandBase() {
			return;
		}

		public virtual void Dispose() {
			// clear the cache
			return;
		}

		#endregion


		#region methods

		public void Run(string[] args) {
			using (Proxy proxy = new Proxy(null)) {
				proxy.CredentialCallback = GetCredential;
				proxy.Start();
				Console.WriteLine("Listening...");
				Console.WriteLine("Press Ctrl+C to quit.");
				using (ManualResetEvent quitEvent = new ManualResetEvent(false)) {
					ConsoleCancelEventHandler ctrlCHandler = (o, e) => {
						e.Cancel = true;
						quitEvent.Set();
					};
					Console.CancelKeyPress += ctrlCHandler;
					quitEvent.WaitOne();
					Console.CancelKeyPress -= ctrlCHandler;
				}
				bool b = proxy.Stop(5000);
				Console.WriteLine(b? "Completed.": "Not Completed.");
			}

			return;
		}

		#endregion


		#region privates

		private static NetworkCredential GetCredential(string realm) {
			Console.WriteLine($"Credential for {realm} is required.");
			Console.Write("UserName: ");
			string userName = Console.ReadLine();
			Console.Write("Password: ");
			string password = GetPassword();

			return new NetworkCredential(userName, password);
		}

		// thanks to http://stackoverflow.com/questions/3404421/password-masking-console-application
		private static string GetPassword() {
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
