using System;
using System.Diagnostics;
using MAPE.Utils;


namespace MAPE.Command.Settings {
	public class GUISettings: MAPE.Utils.Settings {
		#region types

		public static class SettingNames {
			#region constants

			public const string Start = "Start";

			#endregion
		}

		public static class Defaults {
			#region constants

			public const bool Start = false;

			#endregion
		}

		#endregion


		#region data

		public bool Start { get; set; }

		#endregion


		#region creation and disposal

		public GUISettings(IObjectData data): base(data) {
			// prepare settings
			bool start = Defaults.Start;
			if (data != null) {
				// get settings from data
				start = data.GetBooleanValue(SettingNames.Start, start);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.Start = start;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public GUISettings(): this(NullObjectData) {
		}

		public GUISettings(GUISettings src) : base(src) {
			// argument checks
			if (src == null) {
				throw new ArgumentNullException(nameof(src));
			}

			// clone members
			this.Start = src.Start;

			return;
		}

		#endregion


		#region overrides/overridables

		protected override MAPE.Utils.Settings Clone() {
			return new GUISettings(this);
		}

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// save settings
			data.SetBooleanValue(SettingNames.Start, this.Start, omitDefault, this.Start == Defaults.Start);

			return;
		}

		#endregion
	}
}
