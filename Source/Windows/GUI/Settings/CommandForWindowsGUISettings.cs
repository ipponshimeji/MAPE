using System;
using System.Diagnostics;
using MAPE.Utils;
using MAPE.Command.Settings;
using MAPE.Windows.Settings;


namespace MAPE.Windows.GUI.Settings {
	public class CommandForWindowsGUISettings: CommandForWindowsSettings {
		#region properties

		public new GUIForWindowsGUISettings GUI {
			get {
				return (GUIForWindowsGUISettings)base.GUI;
			}
		}

		#endregion


		#region creation and disposal

		public CommandForWindowsGUISettings(IObjectData data): base(data) {
		}

		public CommandForWindowsGUISettings(): this(NullObjectData) {
		}

		public CommandForWindowsGUISettings(CommandForWindowsGUISettings src) : base(src) {
		}

		#endregion


		#region overrides/overridables

		protected override MAPE.Utils.Settings Clone() {
			return new CommandForWindowsGUISettings(this);
		}

		protected override GUISettings CreateGUISettings(IObjectData data) {
			// argument checks
			// data can be null

			return new GUIForWindowsGUISettings(data);
		}

		#endregion
	}
}
