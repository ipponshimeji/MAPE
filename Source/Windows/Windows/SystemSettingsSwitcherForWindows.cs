using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using MAPE.Server;
using MAPE.Command;
using MAPE.Windows.Settings;


namespace MAPE.Windows {
	public class SystemSettingsSwitcherForWindows: SystemSettingsSwitcher {
		#region types

		public static class RegistryNames {
			#region constants

			public const string AutoConfigURL = "AutoConfigURL";

			public const string ProxyEnable = "ProxyEnable";

			public const string ProxyServer = "ProxyServer";

			public const string ProxyOverride = "ProxyOverride";

			public const string DefaultConnectionSettings = "DefaultConnectionSettings";

			#endregion
		}

		public static class EnvironmentNames {
			#region constants

			public const string HttpProxy = "http_proxy";

			public const string HttpsProxy = "https_proxy";

			#endregion
		}

		#endregion


		#region constants

		public const int ConnectionsRevisionIndex = 4;

		public const int AutoDetectByteIndex = 8;

		public const byte AutoDetectFlag = 0x08;

		#endregion


		#region data

		public string ProxyOverride { get; protected set; } = null;

		#endregion


		#region creation and disposal

		public SystemSettingsSwitcherForWindows(CommandBase owner, SystemSettingsSwitcherForWindowsSettings settings): base(owner, settings) {
			// argument checks
			// settings can be null

			if (settings == null) {
				// simple initialization (ex. to restore)
				Debug.Assert(this.ProxyOverride == null);
			} else {
				// usual initialization
				this.ProxyOverride = settings.ProxyOverride;
			}

			return;
		}

		#endregion


		#region overridables

		protected override SystemSettings CreateSystemSettings() {
			return new SystemSettingsForWindows();
		}

		protected override void SetCurrentSystemSettingsTo(SystemSettings settings) {
			// argument checks
			Debug.Assert(settings != null);
			SystemSettingsForWindows actualSettings = settings as SystemSettingsForWindows;
			if (actualSettings == null) {
				throw CreateArgumentIsNotSystemSettingsForWindowsException(nameof(settings));
			}

			// set the base class level settings
			base.SetCurrentSystemSettingsTo(settings);

			// set this class level settings
			// read Internet Options from the registry 
			using (RegistryKey key = OpenInternetSettingsKey(writable: false)) {
				// AutoConfigURL
				actualSettings.AutoConfigURL = (string)key.GetValue(RegistryNames.AutoConfigURL, defaultValue: null);

				// ProxyEnable
				actualSettings.ProxyEnable = (int?)key.GetValue(RegistryNames.ProxyEnable, defaultValue: null);

				// ProxyServer
				actualSettings.ProxyServer = (string)key.GetValue(RegistryNames.ProxyServer, defaultValue: null);

				// ProxyOverride
				actualSettings.ProxyOverride = (string)key.GetValue(RegistryNames.ProxyOverride, defaultValue: null);

				// AutoDetect
				using (RegistryKey connectionsKey = OpenConnectionsKey(key, writable: false)) {
					bool autoDetect = false;
					byte[] bytes = (byte[])connectionsKey.GetValue(RegistryNames.DefaultConnectionSettings, defaultValue: null);
					if (bytes != null && AutoDetectByteIndex < bytes.Length) {
						autoDetect = (bytes[AutoDetectByteIndex] & AutoDetectFlag) != 0;
					}
					actualSettings.AutoDetect = autoDetect;
				}
			}

			// read User Environment Variables from the registry
			using (RegistryKey key = OpenEnvironmentKey(writable: false)) {
				// HttpProxyEnvironmentVariable
				actualSettings.HttpProxyEnvironmentVariable = (string)key.GetValue(EnvironmentNames.HttpProxy, defaultValue: null);

				// HttpsProxyEnvironmentVariable
				actualSettings.HttpsProxyEnvironmentVariable = (string)key.GetValue(EnvironmentNames.HttpsProxy, defaultValue: null);
			}

			return;
		}

		protected override void SetSwitchingSystemSettingsTo(SystemSettings settings, Proxy proxy) {
			// argument checks
			Debug.Assert(settings != null);
			SystemSettingsForWindows actualSettings = settings as SystemSettingsForWindows;
			if (actualSettings == null) {
				throw CreateArgumentIsNotSystemSettingsForWindowsException(nameof(settings));
			}
			Debug.Assert(proxy != null);

			// set the base class level settings
			base.SetSwitchingSystemSettingsTo(settings, proxy);

			// set this class level settings
			string proxyEndPoint = proxy.MainListenerEndPoint.ToString();

			Debug.Assert(actualSettings.AutoConfigURL == null);
			actualSettings.ProxyEnable = 1;
			actualSettings.ProxyServer = $"http={proxyEndPoint};https={proxyEndPoint}";
			actualSettings.ProxyOverride = this.ProxyOverride;
			Debug.Assert(actualSettings.AutoDetect == false);
			actualSettings.HttpProxyEnvironmentVariable = $"http://{proxyEndPoint}";
			actualSettings.HttpsProxyEnvironmentVariable = $"http://{proxyEndPoint}";

			return;
		}

		protected override bool SwitchTo(SystemSettings settings, SystemSettings backup) {
			// argument checks
			Debug.Assert(settings != null);
			SystemSettingsForWindows actualSettings = settings as SystemSettingsForWindows;
			if (actualSettings == null) {
				throw CreateArgumentIsNotSystemSettingsForWindowsException(nameof(settings));
			}
			// backup can be null
			SystemSettingsForWindows actualBackup = backup as SystemSettingsForWindows;
			if (backup != null && actualBackup == null) {
				throw CreateArgumentIsNotSystemSettingsForWindowsException(nameof(backup));
			}

			// adjust settings
			string proxyOverride = actualSettings.ProxyOverride;
			if (actualBackup != null && string.IsNullOrEmpty(actualBackup.ProxyOverride) == false) {
				// use the current ProxyOverride if it is defined explicitly
				proxyOverride = actualBackup.ProxyOverride;
			}

			// set Internet Options in the registry 
			using (RegistryKey key = OpenInternetSettingsKey(writable: true)) {
				// AutoConfigURL
				SetValue(key, RegistryNames.AutoConfigURL, actualSettings.AutoConfigURL);

				// ProxyEnable
				SetValue(key, RegistryNames.ProxyEnable, actualSettings.ProxyEnable);

				// ProxyServer
				SetValue(key, RegistryNames.ProxyServer, actualSettings.ProxyServer);

				// ProxyOverride
				SetValue(key, RegistryNames.ProxyOverride, proxyOverride);

				// AutoDetect
				using (RegistryKey connectionsKey = OpenConnectionsKey(key, writable: true)) {
					byte[] bytes = (byte[])connectionsKey.GetValue(RegistryNames.DefaultConnectionSettings, defaultValue: null);
					if (bytes != null && AutoDetectByteIndex < bytes.Length) {
						byte oldFlags = bytes[AutoDetectByteIndex];
						byte newFlags = actualSettings.AutoDetect ? (byte)(oldFlags | AutoDetectFlag) : (byte)(oldFlags & ~AutoDetectFlag);
						if (oldFlags != newFlags) {
							// set AutoDetect flag
							bytes[AutoDetectByteIndex] = newFlags;

							// update revision
							// Do not increment the revision.
							// Incrementing revision confuses Windows' Internet Option System.
							// It seems that it detects difference based on this value.
#if false
							uint revision = BitConverter.ToUInt32(bytes, ConnectionsRevisionIndex);
							byte[] revisionBytes = BitConverter.GetBytes(++revision);
							Debug.Assert(revisionBytes.Length == 4);
							Array.Copy(revisionBytes, 0, bytes, ConnectionsRevisionIndex, 4);
#endif

							// save the bytes
							connectionsKey.SetValue(RegistryNames.DefaultConnectionSettings, bytes, RegistryValueKind.Binary);
						}
					}
				}
			}

			// set User Environment Variables in the registry
			using (RegistryKey key = OpenEnvironmentKey(writable: true)) {
				// HttpProxyEnvironmentVariable
				SetValue(key, EnvironmentNames.HttpProxy, actualSettings.HttpProxyEnvironmentVariable);

				// HttpsProxyEnvironmentVariable
				SetValue(key, EnvironmentNames.HttpsProxy, actualSettings.HttpsProxyEnvironmentVariable);
			}

			return true;
		}

		protected override void NotifySwitched() {
			// notify the internet settings are changed
			InternetSetOption(
				IntPtr.Zero,    // NULL
				39,             // INTERNET_OPTION_SETTINGS_CHANGED
				IntPtr.Zero,    // NULL
				0
			);

			// notify that environment variables are changed
			UIntPtr dummy;
			SendMessageTimeout(
				(IntPtr)0xffff,     // HWND_BROADCAST
				0x001A,             // WM_SETTINGCHANGE
				UIntPtr.Zero,
				"Environment",
				0x0002,             // SMTO_ABORTIFHUNG,
				5000,
				out dummy
			);

			return;
		}

		#endregion


		#region private

		private static ArgumentException CreateArgumentIsNotSystemSettingsForWindowsException(string argName) {
			throw new ArgumentException($"It must be an instance of {nameof(SystemSettingsForWindows)} class.", argName);
		}

		private static RegistryKey OpenInternetSettingsKey(bool writable) {
			return Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings", writable);
		}

		private static RegistryKey OpenConnectionsKey(RegistryKey internetSettingsKey, bool writable) {
			// argument checks
			Debug.Assert(internetSettingsKey != null);

			return internetSettingsKey.OpenSubKey("Connections", writable);
		}

		private static RegistryKey OpenEnvironmentKey(bool writable) {
			return Registry.CurrentUser.OpenSubKey(@"Environment", writable);
		}

		private static void SetValue(RegistryKey key, string name, string value) {
			// argument checks
			Debug.Assert(key != null);
			// name can be null or empty (that means the default value)
			// value can be null

			if (value == null) {
				key.DeleteValue(name, throwOnMissingValue: false);
			} else {
				key.SetValue(name, value, RegistryValueKind.String);
			}
		}

		private static void SetValue(RegistryKey key, string name, int? value) {
			// argument checks
			Debug.Assert(key != null);
			// name can be null or empty (that means the default value)
			// value can be null

			if (value == null) {
				key.DeleteValue(name, throwOnMissingValue: false);
			} else {
				key.SetValue(name, value.Value, RegistryValueKind.DWord);
			}
		}

		#endregion


		#region interop entries

		// in our use, lParam is used as string.
		[DllImport("User32.dll", SetLastError = true)]
		public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

		[DllImport("wininet.dll", SetLastError = true)]
		private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int lpdwBufferLength);

		#endregion
	}
}
