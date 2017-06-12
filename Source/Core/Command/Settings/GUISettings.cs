using System;
using System.Diagnostics;
using MAPE.Utils;


namespace MAPE.Command.Settings {
	public class GUISettings: MAPE.Utils.Settings {
		#region types

		public static class SettingNames {
			#region constants

			public const string Start = "Start";

			public const string ResumeTryCount = "ResumeTryCount";

			public const string ResumeDelay = "ResumeDelay";

			public const string ResumeInterval = "ResumeInterval";

			#endregion
		}

		public static class Defaults {
			#region constants

			public const bool Start = false;

			public const int ResumeTryCount = 2;

			public const int ResumeDelay = 10000;

			public const int ResumeInterval = 3000;

			#endregion
		}

		#endregion


		#region data

		public bool Start { get; set; }

		public int ResumeTryCount { get; set; }

		public int ResumeDelay { get; set; }

		public int ResumeInterval { get; set; }

		#endregion


		#region creation and disposal

		public GUISettings(IObjectData data): base(data) {
			// prepare settings
			bool start = Defaults.Start;
			int resumeTryCount = Defaults.ResumeTryCount;
			int resumeDelay = Defaults.ResumeDelay;
			int resumeInterval = Defaults.ResumeInterval;
			if (data != null) {
				// get settings from data
				start = data.GetBooleanValue(SettingNames.Start, start);
				resumeTryCount = data.GetInt32Value(SettingNames.ResumeTryCount, resumeTryCount);
				resumeDelay = data.GetInt32Value(SettingNames.ResumeDelay, resumeDelay);
				resumeInterval = data.GetInt32Value(SettingNames.ResumeInterval, resumeInterval);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.Start = start;
				this.ResumeTryCount = resumeTryCount;
				this.ResumeDelay = resumeDelay;
				this.ResumeInterval = resumeInterval;
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
			this.ResumeTryCount = src.ResumeTryCount;
			this.ResumeDelay = src.ResumeDelay;
			this.ResumeInterval = src.ResumeInterval;

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
			data.SetInt32Value(SettingNames.ResumeTryCount, this.ResumeTryCount, omitDefault, this.ResumeTryCount == Defaults.ResumeTryCount);
			data.SetInt32Value(SettingNames.ResumeDelay, this.ResumeDelay, omitDefault, this.ResumeDelay == Defaults.ResumeDelay);
			data.SetInt32Value(SettingNames.ResumeInterval, this.ResumeInterval, omitDefault, this.ResumeInterval == Defaults.ResumeInterval);

			return;
		}

		#endregion
	}
}
