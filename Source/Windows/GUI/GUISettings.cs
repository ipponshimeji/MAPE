using System;
using System.Diagnostics;
using System.Windows;
using MAPE.Utils;


namespace MAPE.Windows.GUI {
	internal static class OldGUISettings {
		#region types

		public static class SettingNames {
			#region constants

			public const string ChaseLastLog = "ChaseLastLog";

			public const string MainWindow = "MainWindow";
			public const string LogListViewColumnWidths = "LogListViewColumnWidths";
			public const string Placement = "Placement";
			public const string Flags = "Flags";
			public const string ShowCmd = "ShowCmd";
			public const string MinPosition = "MinPosition";
			public const string MaxPosition = "MaxPosition";
			public const string NormalPosition = "NormalPosition";
			public const string Left = "Left";
			public const string Top = "Top";
			public const string Right = "Right";
			public const string Bottom = "Bottom";
			public const string X = "X";
			public const string Y = "Y";

			#endregion
		}

		#endregion


		#region methods

		public static NativeMethods.POINT GetPOINTValue(this SettingsData settings, string settingName, NativeMethods.POINT defaultValue) {
			SettingsData.Value value = settings.GetValue(settingName);
			if (value.IsNull) {
				return defaultValue;
			} else {
				SettingsData subSettings = value.GetObjectValue();
				int x = subSettings.GetInt32Value(SettingNames.X, defaultValue.X);
				int y = subSettings.GetInt32Value(SettingNames.Y, defaultValue.Y);

				return new NativeMethods.POINT(x, y);
			}
		}

		public static void SetPOINTValue(this SettingsData settings, string settingName, NativeMethods.POINT value, bool omitDefault, NativeMethods.POINT defaultValue = default(NativeMethods.POINT)) {
			if (omitDefault && value == defaultValue) {
				settings.RemoveValue(settingName);
			} else {
				SettingsData subSettings = settings.GetObjectValue(settingName, SettingsData.EmptySettingsGenerator, createIfNotExist: true);
				subSettings.SetInt32Value(SettingNames.X, value.X);
				subSettings.SetInt32Value(SettingNames.Y, value.Y);
			}
		}

		public static NativeMethods.RECT GetRECTValue(this SettingsData settings, string settingName, NativeMethods.RECT defaultValue) {
			SettingsData.Value value = settings.GetValue(settingName);
			if (value.IsNull) {
				return defaultValue;
			} else {
				SettingsData subSettings = value.GetObjectValue();
				int left = subSettings.GetInt32Value(SettingNames.Left, defaultValue.Left);
				int top = subSettings.GetInt32Value(SettingNames.Top, defaultValue.Top);
				int right = subSettings.GetInt32Value(SettingNames.Right, defaultValue.Right);
				int bottom = subSettings.GetInt32Value(SettingNames.Bottom, defaultValue.Bottom);

				return new NativeMethods.RECT(left, top, right, bottom);
			}
		}

		public static void SetRECTValue(this SettingsData settings, string settingName, NativeMethods.RECT value, bool omitDefault, NativeMethods.RECT defaultValue = default(NativeMethods.RECT)) {
			if (omitDefault && value == defaultValue) {
				settings.RemoveValue(settingName);
			} else {
				SettingsData subSettings = settings.GetObjectValue(settingName, SettingsData.EmptySettingsGenerator, createIfNotExist: true);
				subSettings.SetInt32Value(SettingNames.Left, value.Left);
				subSettings.SetInt32Value(SettingNames.Top, value.Top);
				subSettings.SetInt32Value(SettingNames.Right, value.Right);
				subSettings.SetInt32Value(SettingNames.Bottom, value.Bottom);
			}
		}


		public static NativeMethods.WINDOWPLACEMENT? GetWINDOWPLACEMENTValue(this SettingsData settings, string settingName) {
			SettingsData.Value value = settings.GetValue(settingName);
			if (value.IsNull) {
				return null;
			} else {
				SettingsData subSettings = value.GetObjectValue();
				NativeMethods.WINDOWPLACEMENT wp = new NativeMethods.WINDOWPLACEMENT();
				wp.Flags = subSettings.GetInt32Value(SettingNames.Flags, wp.Flags);
				wp.ShowCmd = subSettings.GetInt32Value(SettingNames.ShowCmd, wp.ShowCmd);
				wp.MinPosition = subSettings.GetPOINTValue(SettingNames.MinPosition, wp.MinPosition);
				wp.MaxPosition = subSettings.GetPOINTValue(SettingNames.MaxPosition, wp.MaxPosition);
				wp.NormalPosition = subSettings.GetRECTValue(SettingNames.NormalPosition, wp.NormalPosition);

				return wp;
			}
		}

		public static void SetWINDOWPLACEMENTValue(this SettingsData settings, string settingName, NativeMethods.WINDOWPLACEMENT? value, bool omitDefault) {
			if (omitDefault && value == null) {
				settings.RemoveValue(settingName);
			} else {
				NativeMethods.WINDOWPLACEMENT actualValue = value.HasValue ? value.Value : new NativeMethods.WINDOWPLACEMENT();
				SettingsData subSettings = settings.GetObjectValue(settingName, SettingsData.EmptySettingsGenerator, createIfNotExist: true);
				subSettings.SetInt32Value(SettingNames.Flags, actualValue.Flags);
				subSettings.SetInt32Value(SettingNames.ShowCmd, actualValue.ShowCmd);
				subSettings.SetPOINTValue(SettingNames.MinPosition, actualValue.MinPosition, omitDefault: false);
				subSettings.SetPOINTValue(SettingNames.MaxPosition, actualValue.MaxPosition, omitDefault: false);
				subSettings.SetRECTValue(SettingNames.NormalPosition, actualValue.NormalPosition, omitDefault: false);
			}
		}

		#endregion
	}
}
