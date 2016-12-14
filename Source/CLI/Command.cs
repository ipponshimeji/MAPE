using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MAPE.Core;


namespace CLI {
	class Command: CommandBase {
		#region entry point

		static void Main(string[] args) {
			try {
				using (Command command = new Command()) {
					command.Run(args);
				}
			} catch (Exception exception) {
				Console.Error.WriteLine(exception);
			}

			return;
		}

		#endregion
	}
}
