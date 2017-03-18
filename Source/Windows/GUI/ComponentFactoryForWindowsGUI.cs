using System;
using MAPE.Utils;
using MAPE.Command.Settings;


namespace MAPE.Windows.GUI {
    public class ComponentFactoryForWindowsGUI: ComponentFactoryForWindows {
		#region methods

		public override CommandSettings CreateCommandSettings(IObjectData data) {
			return new Settings.CommandForWindowsGUISettings(data);
		}

		#endregion
	}
}
