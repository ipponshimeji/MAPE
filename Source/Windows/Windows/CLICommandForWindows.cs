using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using MAPE.Command;
using MAPE.Utils;
using MAPE.Command.Settings;
using MAPE.Windows.Settings;


namespace MAPE.Windows {
    public abstract class CLICommandForWindows: CLICommandBase {
		#region types

		public static new class OptionNames {
			#region constants

			public const string ProxyOverride = "ProxyOverride";

			#endregion
		}

		#endregion


		#region creation and disposal

		public CLICommandForWindows(ComponentFactoryForWindows componentFactory): base(componentFactory) {
			return;
		}

		#endregion


		#region overrides/overridables - argument processing

		protected override bool HandleOption(string name, string value, CommandSettings settings) {
			// handle option
			bool handled = true;
			if (AreSameOptionNames(name, OptionNames.ProxyOverride)) {
				SystemSettingsSwitcherForWindowsSettings actualSettings = (SystemSettingsSwitcherForWindowsSettings)settings.SystemSettingsSwitcher;
				actualSettings.ProxyOverride = value;
			} else {
				handled = base.HandleOption(name, value, settings);
			}

			return handled;
		}

		#endregion


		#region overrides/overridables - execution

		protected override void RunProxyImpl(CommandSettings settings) {
			// prepare Windows system event handlers
			SessionEndingEventHandler onSessionEnding = (o, e) => {
				AwakeControllerThread(ControllerThreadEventKind.Quit);
			};
			PowerModeChangedEventHandler onPowerModeChanged = (o, e) => {
				switch (e.Mode) {
					case PowerModes.Suspend:
						AwakeControllerThread(ControllerThreadEventKind.Suspend);
						break;
					case PowerModes.Resume:
						AwakeControllerThread(ControllerThreadEventKind.Resume);
						break;
				}
			};

			// run the proxy
			SystemEvents.SessionEnding += onSessionEnding;
			SystemEvents.PowerModeChanged += onPowerModeChanged;
			try {
				base.RunProxyImpl(settings);
			} finally {
				SystemEvents.PowerModeChanged -= onPowerModeChanged;
				SystemEvents.SessionEnding -= onSessionEnding;
			}

			return;
		}

		protected override void BringAppToForeground() {
			SetForegroundWindow(GetConsoleWindow());
		}

		#endregion


		#region interops

		[DllImport("kernel32.dll", ExactSpelling = true)]
		private static extern IntPtr GetConsoleWindow();

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetForegroundWindow(IntPtr hWnd);

		#endregion
	}
}
