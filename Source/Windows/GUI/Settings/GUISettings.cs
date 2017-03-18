using System;
using System.Diagnostics;
using MAPE.Utils;


namespace MAPE.Windows.GUI.Settings {
	public class GUISettings: MAPE.Utils.Settings {
		#region types

		public static class SettingNames {
			#region constants

			public const string ChaseLastLog = "ChaseLastLog";

			public const string MainWindow = "MainWindow";

			#endregion
		}

		public static class Defaults {
			#region constants

			public const bool ChaseLastLog = true;

			#endregion
		}

		#endregion


		#region data

		public bool ChaseLastLog { get; set; }

		private MainWindowSettings mainWindow;

		#endregion


		#region properties

		public MainWindowSettings MainWindow {
			get {
				return this.mainWindow;
			}
			set {
				if (value == null) {
					throw new ArgumentNullException(nameof(value));
				}

				this.mainWindow = value;
			}
		}

		#endregion


		#region creation and disposal

		public GUISettings(IObjectData data): base(data) {
			// prepare settings
			bool chaseLastLog = Defaults.ChaseLastLog;
			MainWindowSettings mainWindow = null;
			if (data != null) {
				// get settings from data
				chaseLastLog = data.GetBooleanValue(SettingNames.ChaseLastLog, chaseLastLog);
				mainWindow = data.GetObjectValue(SettingNames.MainWindow, mainWindow, this.CreateMainWindowSettings);
			}
			if (mainWindow == null) {
				mainWindow = CreateMainWindowSettings(null);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.ChaseLastLog = chaseLastLog;
				this.mainWindow = mainWindow;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public GUISettings(): this(null) {
		}

		#endregion


		#region overrides/overridables

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// state checks
			Debug.Assert(this.MainWindow != null);

			// save the settings
			data.SetBooleanValue(SettingNames.ChaseLastLog, this.ChaseLastLog, omitDefault, this.ChaseLastLog == Defaults.ChaseLastLog);
			data.SetObjectValue(SettingNames.MainWindow, this.MainWindow, true, omitDefault, false);    // overwrite existing settings, not omittable

			return;
		}

		protected virtual MainWindowSettings CreateMainWindowSettings(IObjectData data) {
			// argument checks
			// data can be null

			return new MainWindowSettings(data);
		}

		#endregion
	}
}
