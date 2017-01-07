using System;
using Microsoft.Win32;
using MAPE.Command;
using MAPE.Server;


namespace MAPE.Windows {
    public class CLICommandForWindows: CLICommandBase {
		#region creation and disposal

		public CLICommandForWindows(ComponentFactoryForWindows componentFactory): base(componentFactory) {
			return;
		}

		#endregion
	}
}
