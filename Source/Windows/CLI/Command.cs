using System;
using MAPE.Utils;
using MAPE.Command.Settings;
using MAPE.Windows;
using MAPE.Windows.CLI.Properties;


namespace MAPE.Windows.CLI {
	class Command: CLICommandForWindows {
		#region entry point

		static void Main(string[] args) {
			using (Command command = new Command()) {
				command.Run(args);
			}

			return;
		}

		#endregion


		#region creation and disposal

		public Command(): base(new ComponentFactoryForWindows()) {
			// initialize members
			this.ComponentName = "CLI command";

			return;
		}

		#endregion


		#region overrides/overridables - execution

		protected override void ShowUsage(CommandSettings settings) {
			Console.WriteLine(Resources.Command_Usage);
		}

		protected override void OutputLogo() {
			OutputStandardLogo(typeof(Command).Assembly);
		}

		#endregion
	}
}
