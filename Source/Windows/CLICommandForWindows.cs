using System;
using MAPE.Command;


namespace MAPE.Windows {
    public class CLICommandForWindows: CLICommandBase {
		#region creation and disposal

		public CLICommandForWindows(ComponentFactoryForWindows componentFactory): base(componentFactory) {
			return;
		}

		#endregion
	}
}
