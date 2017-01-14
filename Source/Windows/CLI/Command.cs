using System;
using MAPE.Windows;


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
	}
}
