using System;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Windows;
using MAPE.Utils;
using MAPE.Command;


namespace MAPE.Windows.GUI {
	internal class Command: GUICommandBase {
		#region data

		private App app = null;

		#endregion


		#region creation and disposal

		public Command(): base(new ComponentFactoryForWindows()) {
			// initialize members
			this.ComponentName = "MAPE GUI";

			return;
		}

		#endregion


		#region overrides/overridables - execution

		protected override void ShowUsage(Settings settings) {
			// show the Usage page in the browser
			Process.Start(GetUsagePagePath());
		}

		protected override void RunProxy(Settings settings) {
			// state checks
			if (this.app != null) {
				throw new InvalidOperationException();
			}

			// run WPF application
			App app = new App(this);
			app.InitializeComponent();
			this.app = app;
			try {
				app.Run();
			} finally {
				this.app = null;
			}

			return;
		}

		protected override CredentialInfo UpdateCredential(string endPoint, string realm, CredentialInfo oldCredential) {
			// argument checks
			Debug.Assert(endPoint != null);
			Debug.Assert(realm != null);    // may be empty
			// oldCredential can be null

			// state checks
			Debug.Assert(this.app != null);

			// ask user's credential
			Func<CredentialInfo> callback = () => {
				// setup a credential dialog
				CredentialDialog dialog = new CredentialDialog();
				dialog.Title = realm;
				dialog.EndPoint = endPoint;
				dialog.Credential = oldCredential;

				// show the credential dialog and get user input
				return (dialog.ShowDialog() ?? false) ? dialog.Credential : null;
			};

			return this.app.Dispatcher.Invoke<CredentialInfo>(callback);
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
				if (File.Exists(filePath)) {
					return filePath;
				}

				culture = culture.Parent;
			}

			// returns the usage page for the neutral locale
			return Path.Combine(folderPath, "Usage.html");
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
