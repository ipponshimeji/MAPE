using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MAPE.Utils;
using MAPE.Server;


namespace MAPE.Windows {
	public class AutoConfigActualProxy: IActualProxy {
		#region constants

		public const int DefaultTimeout = 60000;    // 60 seconds, same to the .NET framework implementation

		protected const char ProxySeparator = ';';

		#endregion


		#region data

		private bool useAutoDetection = false;

		private string autoConfigUrl = null;

		private SafeHINTERNET session = null;

		#endregion


		#region creation & disposal

		public AutoConfigActualProxy(bool useAutoDetection, string autoConfigUrl, int timeout = DefaultTimeout) {
			// argument checks
			// autoConfigUrl can be null
			if (useAutoDetection == false && string.IsNullOrEmpty(autoConfigUrl)) {
				throw new ArgumentException($"Either auto detection mode or auto config file mode must be enabled.");
			}

			// open a WinHttp session
			// give settings for downloading the PAC file: no user agent and no proxy.
			SafeHINTERNET handle = WinHttpOpen(null, WINHTTP_ACCESS_TYPE_NO_PROXY, null, null, 0);
			if (handle == null || handle.IsInvalid) {
				LogWin32Error("WinHttpOpen");
				throw new ApplicationException("");		// ToDo: message
			}

			// set the timeout
			if (WinHttpSetTimeouts(handle, timeout, timeout, timeout, timeout) == false) {
				LogWin32Error("WinHttpSetTimeouts");
				// continue (not fatal)
			}

			// initialize members
			this.useAutoDetection = useAutoDetection;
			this.autoConfigUrl = autoConfigUrl;
			this.session = handle;

			return;
		}

		public void Dispose() {
			// close the session handle
			SafeHINTERNET handle = this.session;
			this.session = null;
			if (handle != null) {
				handle.Close();
			}

			return;
		}

		#endregion


		#region IActualProxy

		public string Description {
			get {
				string autoDetect = string.Empty;
				if (this.useAutoDetection) {
					autoDetect = "auto detection";
				}

				string autoConfig = string.Empty;
				if (string.IsNullOrEmpty(this.autoConfigUrl) == false) {
					autoConfig = $"auto config file '{this.autoConfigUrl}'";
				}

				string separator = string.Empty;
				if (0 < autoDetect.Length && 0 < autoConfig.Length) {
					separator = " or ";
				}

				return string.Concat(autoDetect, separator, autoConfig);
			}
		}

		public IReadOnlyCollection<DnsEndPoint> GetProxyEndPoints(DnsEndPoint targetEndPoint) {
			// argument checks
			if (targetEndPoint == null) {
				throw new ArgumentNullException(nameof(targetEndPoint));
			}

			// get the found proxie end points
			// the target url is assumed as a https url
			return GetProxiesForUrl($"https://{targetEndPoint.Host}:{targetEndPoint.Port}/");
		}

		public IReadOnlyCollection<DnsEndPoint> GetProxyEndPoints(Uri targetUri) {
			// argument checks
			if (targetUri == null) {
				throw new ArgumentNullException(nameof(targetUri));
			}

			// get the found proxy end points
			return GetProxiesForUrl(targetUri.ToString());
		}

		#endregion


		#region privates

		private DnsEndPoint[] GetProxiesForUrl(string targetUrl) {
			// get proxies
			string proxies;
			int win32Error = GetProxiesForUrl(targetUrl, out proxies);
			if (win32Error != ERROR_SUCCESS || string.IsNullOrEmpty(proxies)) {
				return null;
			}
			Debug.Assert(proxies != null);

			// convert proxies to DnsEndPoint[]
			return (
				from proxy in proxies.Split(ProxySeparator)
				select Util.ParseEndPoint(proxy.Trim())
			).ToArray();
		}

		private int GetProxiesForUrl(string targetUrl, out string proxies) {
			// argument checks
			Debug.Assert(string.IsNullOrEmpty(targetUrl) == false);

			// prepare options
			WINHTTP_AUTOPROXY_OPTIONS options = new WINHTTP_AUTOPROXY_OPTIONS();
			Debug.Assert(options.dwFlags == 0);
			Debug.Assert(options.dwAutoDetectFlags == 0);
			Debug.Assert(options.lpszAutoConfigUrl == null);

			if (this.useAutoDetection) {
				// use auto detection
				options.dwFlags |= WINHTTP_AUTOPROXY_AUTO_DETECT;
				options.dwAutoDetectFlags = WINHTTP_AUTO_DETECT_TYPE_DHCP | WINHTTP_AUTO_DETECT_TYPE_DNS_A;
			}

			if (string.IsNullOrEmpty(this.autoConfigUrl) == false) {
				// use auto config file
				options.dwFlags |= WINHTTP_AUTOPROXY_CONFIG_URL;
				options.lpszAutoConfigUrl = this.autoConfigUrl;
			}

			// first, try to get proxies without authentication
			options.fAutoLogonIfChallenged = false;
			int win32Error = GetProxiesForUrl(targetUrl, ref options, out proxies);
			if (win32Error == ERROR_WINHTTP_LOGIN_FAILURE) {
				// It seems that you need authentication to download the PAC file.
				// Retry with the auto logon flag.
				// This flag should be used only after the download fails with ERROR_WINHTTP_LOGIN_FAILURE
				// because authentication has a bit of overhead.
				// See https://msdn.microsoft.com/en-us/library/windows/desktop/aa383153.aspx
				options.fAutoLogonIfChallenged = true;
				win32Error = GetProxiesForUrl(targetUrl, ref options, out proxies);
			}
			if (win32Error != ERROR_SUCCESS) {
				LogWin32Error("WinHttpGetProxyForUrl");
			}

			return win32Error;
		}

		private int GetProxiesForUrl(string targetUrl, ref WINHTTP_AUTOPROXY_OPTIONS options, out string proxies) {
			// argument checks
			Debug.Assert(string.IsNullOrEmpty(targetUrl) == false);

			int win32Error = ERROR_SUCCESS;
			WINHTTP_PROXY_INFO info = new WINHTTP_PROXY_INFO();

			// call Win32 WinHttpGetProxyForUrl and convnert the results to managed string
			RuntimeHelpers.PrepareConstrainedRegions();
			try {
				if (WinHttpGetProxyForUrl(this.session, targetUrl, ref options, out info)) {
					proxies = Marshal.PtrToStringUni(info.lpszProxy);
				} else {
					win32Error = GetLastWin32Error();
					proxies = null;
				}
			} finally {
				Marshal.FreeHGlobal(info.lpszProxyBypass);
				Marshal.FreeHGlobal(info.lpszProxy);
			}

			return win32Error;
		}

		private static int GetLastWin32Error() {
			int win32Error = Marshal.GetLastWin32Error();
			if (win32Error == ERROR_NOT_ENOUGH_MEMORY) {
				throw new OutOfMemoryException();
			}

			return win32Error;
		}

		private static void Log(string message) {
			ConsoleColor backup = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.DarkGray;
			try {
				Console.WriteLine(message);
			} finally {
				Console.ForegroundColor = backup;
			}

			return;
		}

		private static void LogWin32Error(string api) {
			int win32Error = GetLastWin32Error();
			Logger.LogError("Win32API", $"error in '{api}'. Win32 Error: {win32Error}.");
		}

		private void EnsureReady() {
			if (this.session == null || this.session.IsInvalid) {
				throw new InvalidOperationException();
			}

			return;
		}

		private string GetFirstProxy(string proxies) {
			// argument checks
			Debug.Assert(string.IsNullOrEmpty(proxies) == false);

			// split and return the first proxy
			string proxy;
			int index = proxies.IndexOf(';');
			if (index < 0) {
				proxy = proxies;
			} else {
				proxy = proxies.Substring(0, index);
			}

			return proxy.Trim();
		}

		#endregion


		#region Win32 interops

		private const string WinHttpDllName = "winhttp.dll";

		// Win32 error codes
		internal const int ERROR_SUCCESS = 0;
		internal const int ERROR_NOT_ENOUGH_MEMORY = 8;
		internal const int ERROR_WINHTTP_INTERNAL_ERROR = 12004;
		internal const int ERROR_WINHTTP_LOGIN_FAILURE = 12015;

		// Flags for WINHTTP_AUTOPROXY_OPTIONS.dwFlags
		internal const int WINHTTP_AUTOPROXY_AUTO_DETECT = 0x00000001;
		internal const int WINHTTP_AUTOPROXY_CONFIG_URL = 0x00000002;

		// Flags for WINHTTP_AUTOPROXY_OPTIONS.dwAutoDetectFlags
		internal const int WINHTTP_AUTO_DETECT_TYPE_DHCP = 1;
		internal const int WINHTTP_AUTO_DETECT_TYPE_DNS_A = 2;

		// Values for dwAccessType in WinHttpOpen() or WINHTTP_PROXY_INFO
		internal const int WINHTTP_ACCESS_TYPE_NO_PROXY = 1;
		internal const int WINHTTP_ACCESS_TYPE_DEFAULT_PROXY = 0;
		internal const int WINHTTP_ACCESS_TYPE_NAMED_PROXY = 3;


		internal sealed class SafeHINTERNET: SafeHandleZeroOrMinusOneIsInvalid {
			public SafeHINTERNET() : base(true) {
			}

			protected override bool ReleaseHandle() {
				return WinHttpCloseHandle(handle);
			}
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct WINHTTP_AUTOPROXY_OPTIONS {
			public int dwFlags;

			public int dwAutoDetectFlags;

			[MarshalAs(UnmanagedType.LPWStr)]
			public string lpszAutoConfigUrl;

			private IntPtr lpvReserved;

			private int dwReserved;

			public bool fAutoLogonIfChallenged;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct WINHTTP_PROXY_INFO {
			public int dwAccessType;

			public IntPtr lpszProxy;

			public IntPtr lpszProxyBypass;
		}


		[DllImport(WinHttpDllName, CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern SafeHINTERNET WinHttpOpen(string pwszUserAgent, int dwAccessType, string pwszProxyName, string pwszProxyBypass, int dwFlags);

		[DllImport(WinHttpDllName, CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern bool WinHttpSetTimeouts(SafeHINTERNET hInternet, int dwResolveTimeout, int dwConnectTimeout, int dwSendTimeout, int dwReceiveTimeout);

		[DllImport(WinHttpDllName, CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern bool WinHttpCloseHandle(IntPtr hInternet);

		[DllImport(WinHttpDllName, CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern bool WinHttpGetProxyForUrl(SafeHINTERNET hSession, string lpcwszUrl, [In] ref WINHTTP_AUTOPROXY_OPTIONS pAutoProxyOptions, out WINHTTP_PROXY_INFO pProxyInfo);

		#endregion
	}
}
