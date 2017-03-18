using System;
using System.Diagnostics;
using MAPE.Utils;
using MAPE.Windows.Settings;


namespace MAPE.Windows.GUI.Settings {
	public class CommandForWindowsGUISettings: CommandForWindowsSettings {
		#region types

		public static new class SettingNames {
			#region constants

			public const string GUI = "GUI";

			#endregion
		}

		#endregion


		#region data

		public GUISettings GUI { get; set; }

		#endregion


		#region creation and disposal

		public CommandForWindowsGUISettings(IObjectData data): base(data) {
			// prepare settings
			GUISettings gui = null;
			if (data != null) {
				// get settings from data
				gui = data.GetObjectValue(SettingNames.GUI, gui, this.CreateGUISettings);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.GUI = gui;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public CommandForWindowsGUISettings(): this(null) {
		}

		#endregion


		#region overrides/overridables

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// save the base class level settings
			base.SaveTo(data, omitDefault);

			// save this class level settings
			data.SetObjectValue(SettingNames.GUI, this.GUI, true, omitDefault, this.GUI == null);   // overwrite existing settings

			return;
		}

		protected virtual GUISettings CreateGUISettings(IObjectData data) {
			// argument checks
			// data can be null

			return new GUISettings(data);
		}

		#endregion
	}
}
