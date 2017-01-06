using System;
using MAPE;
using MAPE.Utils;
using MAPE.Command;


namespace MAPE.Windows {
    public class ComponentFactoryForWindows: ComponentFactory {
		#region methods

		public override RunningProxyState CreateRunningProxyState(CommandBase owner) {
			return new RunningProxyStateForWindows(owner);
		}

		#endregion
	}
}
