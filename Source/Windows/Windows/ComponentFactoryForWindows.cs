using System;
using MAPE;
using MAPE.Utils;
using MAPE.Command;
using MAPE.Server;


namespace MAPE.Windows {
    public class ComponentFactoryForWindows: ComponentFactory {
		#region methods

		public override SystemSettingsSwitcher CreateSystemSettingsSwitcher(CommandBase owner, Settings settings, Proxy proxy) {
			return new SystemSettingsSwitcherForWindows(owner, settings, proxy);
		}

		#endregion
	}
}
