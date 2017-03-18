using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using MAPE.Utils;
using MAPE.Server;
using MAPE.Command;


namespace MAPE.Windows {
	public class SystemSettingsSwitcherForWindows: SystemSettingsSwitcher {
		#region types

		public static new class SettingNames {
			#region constants

			public const string ProxyOverride = "ProxyOverride";

			#endregion
		}

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

		public string AutoConfigURL { get; protected set; } = null;

		public int? ProxyEnable { get; protected set; } = null;

		// ex. http=proxy.example.org:8080;https=proxy.example.org:8080
		public string ProxyServer { get; protected set; } = null;

		// ex. *.example.org;*.example.jp;<local>
		public string ProxyOverride { get; protected set; } = null;

		public bool AutoDetect { get; protected set; } = false;

		public string HttpProxyEnvironmentVariable { get; protected set; } = null;

		public string HttpsProxyEnvironmentVariable { get; protected set; } = null;

		#endregion


		#region creation and disposal

		public SystemSettingsSwitcherForWindows(CommandBase owner, SettingsData settings, Proxy proxy) : base(owner, settings, proxy) {
			// argument checks
			// settings can contain null

			// initialize members
			if (proxy == null) {
				// simple initialization for backup
				// all members are already initialized
			} else {
				// usual initialization
				Debug.Assert(proxy != null);
				string proxyEndPoint = proxy.MainListenerEndPoint.ToString();

				Debug.Assert(this.AutoConfigURL == null);
				this.ProxyEnable = 1;
				this.ProxyServer = $"http={proxyEndPoint};https={proxyEndPoint}";
				this.ProxyOverride = settings.GetStringValue(SettingNames.ProxyOverride, null);
				Debug.Assert(this.AutoDetect == false);
				this.HttpProxyEnvironmentVariable = $"http://{proxyEndPoint}";
				this.HttpsProxyEnvironmentVariable = $"http://{proxyEndPoint}";
			}

			return;
		}

		#endregion


		#region overridables

		protected override void LoadCurrentSettings() {
			// load the base class level settings
			base.LoadCurrentSettings();

			// load this class level settings

			// read Internet Options from the registry 
			using (RegistryKey key = OpenInternetSettingsKey(writable: false)) {
				// AutoConfigURL
				this.AutoConfigURL = (string)key.GetValue(RegistryNames.AutoConfigURL, defaultValue: null);

				// ProxyEnable
				this.ProxyEnable = (int?)key.GetValue(RegistryNames.ProxyEnable, defaultValue: null);

				// ProxyServer
				this.ProxyServer = (string)key.GetValue(RegistryNames.ProxyServer, defaultValue: null);

				// ProxyOverride
				this.ProxyOverride = (string)key.GetValue(RegistryNames.ProxyOverride, defaultValue: null);

				// AutoDetect
				using (RegistryKey connectionsKey = OpenConnectionsKey(key, writable: false)) {
					bool autoDetect = false;
					byte[] bytes = (byte[])connectionsKey.GetValue(RegistryNames.DefaultConnectionSettings, defaultValue: null);
					if (bytes != null && AutoDetectByteIndex < bytes.Length) {
						autoDetect = (bytes[AutoDetectByteIndex] & AutoDetectFlag) != 0;
					}
					this.AutoDetect = autoDetect;
				}
			}

			// read User Environment Variables from the registry
			using (RegistryKey key = OpenEnvironmentKey(writable: false)) {
				// HttpProxyEnvironmentVariable
				this.HttpProxyEnvironmentVariable = (string)key.GetValue(EnvironmentNames.HttpProxy, defaultValue: null);

				// HttpsProxyEnvironmentVariable
				this.HttpsProxyEnvironmentVariable = (string)key.GetValue(EnvironmentNames.HttpsProxy, defaultValue: null);
			}

			return;
		}

		protected override bool Switch(SystemSettingsSwitcher backup) {
			// argument checks
			// backup can be null

			// adjust settings
			SystemSettingsSwitcherForWindows actualBackup = backup as SystemSettingsSwitcherForWindows;
			string proxyOverride = this.ProxyOverride;
			if (actualBackup != null && string.IsNullOrEmpty(actualBackup.ProxyOverride) == false) {
				// use the current ProxyOverride if it is defined explicitly
				proxyOverride = actualBackup.ProxyOverride;
			}

			// set Internet Options in the registry 
			using (RegistryKey key = OpenInternetSettingsKey(writable: true)) {
				// AutoConfigURL
				SetValue(key, RegistryNames.AutoConfigURL, this.AutoConfigURL);

				// ProxyEnable
				SetValue(key, RegistryNames.ProxyEnable, this.ProxyEnable);

				// ProxyServer
				SetValue(key, RegistryNames.ProxyServer, this.ProxyServer);

				// ProxyOverride
				SetValue(key, RegistryNames.ProxyOverride, proxyOverride);

				// AutoDetect
				using (RegistryKey connectionsKey = OpenConnectionsKey(key, writable: true)) {
					byte[] bytes = (byte[])connectionsKey.GetValue(RegistryNames.DefaultConnectionSettings, defaultValue: null);
					if (bytes != null && AutoDetectByteIndex < bytes.Length) {
						byte oldFlags = bytes[AutoDetectByteIndex];
						byte newFlags = this.AutoDetect? (byte)(oldFlags | AutoDetectFlag): (byte)(oldFlags & ~AutoDetectFlag);
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
				SetValue(key, EnvironmentNames.HttpProxy, this.HttpProxyEnvironmentVariable);

				// HttpsProxyEnvironmentVariable
				SetValue(key, EnvironmentNames.HttpsProxy, this.HttpsProxyEnvironmentVariable);
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
