using System;
using System.Diagnostics;
using System.Linq;
using MAPE.Utils;


namespace MAPE.Windows.GUI.Settings {
	public class MainWindowSettings: MAPE.Utils.Settings {
		#region types

		public static class SettingNames {
			#region constants

			public const string LogListViewColumnWidths = "LogListViewColumnWidths";
			public const string Placement = "Placement";
			public const string Flags = "Flags";
			public const string ShowCmd = "ShowCmd";
			public const string MinPosition = "MinPosition";
			public const string MaxPosition = "MaxPosition";
			public const string NormalPosition = "NormalPosition";

			public const string X = "X";
			public const string Y = "Y";
			public const string Left = "Left";
			public const string Top = "Top";
			public const string Right = "Right";
			public const string Bottom = "Bottom";

			#endregion
		}

		public static class Defaults {
			#region constants

			internal static readonly NativeMethods.POINT Point = new NativeMethods.POINT();

			internal static readonly NativeMethods.RECT Rect = new NativeMethods.RECT();

			internal const int Flags = 0;

			internal const int ShowCmd = 0;

			#endregion
		}

		#endregion


		#region data

		public double[] LogListViewColumnWidths { get; set; }

		internal NativeMethods.WINDOWPLACEMENT? Placement { get; set; }

		#endregion


		#region creation and disposal

		public MainWindowSettings(IObjectData data): base(data) {
			// prepare settings
			double[] logListViewColumnWidths = null;
			NativeMethods.WINDOWPLACEMENT? placement = null;
			if (data != null) {
				// get settings from data
				logListViewColumnWidths = data.GetDoubleArrayValue(SettingNames.LogListViewColumnWidths, null);
				placement = data.GetNullableValue(SettingNames.Placement, CreateWINDOWPLACEMENT);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.LogListViewColumnWidths = logListViewColumnWidths;
				this.Placement = placement;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public MainWindowSettings(): this(NullObjectData) {
		}

		public MainWindowSettings(MainWindowSettings src): base(src) {
			// argument checks
			if (src == null) {
				throw new ArgumentNullException(nameof(src));
			}

			// clone members
			this.LogListViewColumnWidths = (src.LogListViewColumnWidths == null)? null: (double[])src.LogListViewColumnWidths.Clone();
			this.Placement = src.Placement;

			return;
		}

		#endregion


		#region overrides/overridables

		protected override MAPE.Utils.Settings Clone() {
			return new MainWindowSettings(this);
		}

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// save the settings
			data.SetDoubleArrayValue(SettingNames.LogListViewColumnWidths, this.LogListViewColumnWidths, omitDefault, this.LogListViewColumnWidths == null);
			data.SetNullableValue(SettingNames.Placement, this.Placement, SaveWINDOWPLACEMENT);

			return;
		}

		#endregion


		#region private

		private static NativeMethods.POINT CreatePOINT(IObjectData data) {
			// argument checks
			Debug.Assert(data != null);

			// create POINT value
			int x = data.GetInt32Value(SettingNames.X, Defaults.Point.X);
			int y = data.GetInt32Value(SettingNames.Y, Defaults.Point.Y);

			return new NativeMethods.POINT(x, y);
		}

		private static void SavePOINT(NativeMethods.POINT value, IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// save POINT value
			// there values are not omitted
			data.SetInt32Value(SettingNames.X, value.X);
			data.SetInt32Value(SettingNames.Y, value.Y);

			return;
		}

		private static NativeMethods.RECT CreateRECT(IObjectData data) {
			// argument checks
			Debug.Assert(data != null);

			// create RECT value
			int left = data.GetInt32Value(SettingNames.Left, Defaults.Rect.Left);
			int top = data.GetInt32Value(SettingNames.Top, Defaults.Rect.Top);
			int right = data.GetInt32Value(SettingNames.Right, Defaults.Rect.Right);
			int bottom = data.GetInt32Value(SettingNames.Bottom, Defaults.Rect.Bottom);

			return new NativeMethods.RECT(left, top, right, bottom);
		}

		private static void SaveRECT(NativeMethods.RECT value, IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// save RECT value
			// there values are not omitted
			data.SetInt32Value(SettingNames.Left, value.Left);
			data.SetInt32Value(SettingNames.Top, value.Top);
			data.SetInt32Value(SettingNames.Right, value.Right);
			data.SetInt32Value(SettingNames.Bottom, value.Bottom);

			return;
		}

		private static NativeMethods.WINDOWPLACEMENT CreateWINDOWPLACEMENT(IObjectData data) {
			// argument checks
			Debug.Assert(data != null);

			// create WINDOWPLACEMENT value
			NativeMethods.WINDOWPLACEMENT wp = new NativeMethods.WINDOWPLACEMENT();
			wp.Flags = data.GetInt32Value(SettingNames.Flags, Defaults.Flags);
			wp.ShowCmd = data.GetInt32Value(SettingNames.ShowCmd, Defaults.ShowCmd);
			wp.MinPosition = data.GetObjectValue(SettingNames.MinPosition, Defaults.Point, CreatePOINT);
			wp.MaxPosition = data.GetObjectValue(SettingNames.MaxPosition, Defaults.Point, CreatePOINT);
			wp.NormalPosition = data.GetObjectValue(SettingNames.NormalPosition, Defaults.Rect, CreateRECT);

			return wp;
		}

		private static void SaveWINDOWPLACEMENT(NativeMethods.WINDOWPLACEMENT value, IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// save WINDOWPLACEMENT value
			// there values are not omitted
			data.SetInt32Value(SettingNames.Flags, value.Flags);
			data.SetInt32Value(SettingNames.ShowCmd, value.ShowCmd);
			data.SetObjectValue(SettingNames.MinPosition, value.MinPosition, SavePOINT);
			data.SetObjectValue(SettingNames.MaxPosition, value.MaxPosition, SavePOINT);
			data.SetObjectValue(SettingNames.NormalPosition, value.NormalPosition, SaveRECT);

			return;
		}

		#endregion
	}
}
