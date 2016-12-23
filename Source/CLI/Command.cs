﻿using System;
using MAPE.Windows;


namespace CLI {
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
			return;
		}

		#endregion
	}
}