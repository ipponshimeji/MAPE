using System;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using MAPE.Utils;
using MAPE.Command;


namespace MAPE.Windows.GUI {
	internal class Command: GUICommandBase {
		#region creation and disposal

		public Command(): base(new ComponentFactoryForWindows()) {
			// initialize members
			this.ObjectName = "MAPE GUI";

			return;
		}

		#endregion


		#region overrides/overridables - execution

		protected override void ShowUsage(Settings settings) {
			// show the Usage page in the browser
			Process.Start(GetUsagePagePath());
		}

		protected override void RunProxy(Settings settings) {
			// run WPF application
			App app = new App(this);
			app.InitializeComponent();
			app.Run();

			return;
		}

		protected override CredentialInfo UpdateCredential(string endPoint, string realm) {
			throw new NotImplementedException();
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
