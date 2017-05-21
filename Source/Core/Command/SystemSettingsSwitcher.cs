using System;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using MAPE.Utils;
using MAPE.Server;
using MAPE.Properties;
using MAPE.Command.Settings;


namespace MAPE.Command {
	public class SystemSettingsSwitcher {
		#region types

		public static class ConfigNames {
			#region constants

			public const string DefaultActualProxyHostName = "DefaultActualProxyHostName";

			public const string DefaultActualProxyPort = "DefaultActualProxyPort";

			public const string TestUrl = "TestUrl";

			#endregion
		}

		#endregion


		#region data

		public readonly CommandBase Owner;

		public bool Enabled { get; protected set; } = true;

		public IWebProxy ActualProxy { get; protected set; } = null;

		#endregion


		#region properties

		/// <summary>
		/// Returns the end point of the actual proxy if it has a static end point.
		/// </summary>
		public DnsEndPoint ActualProxyEndPoint {
			get {
				// ToDo: can improve?
				WebProxy webProxy = this.ActualProxy as WebProxy;
				return (webProxy == null) ? null : new DnsEndPoint(webProxy.Address.Host, webProxy.Address.Port);
			}
		}

		#endregion


		#region creation and disposal

		/// <summary>
		/// 
		/// </summary>
		/// <param name="owner"></param>
		/// <param name="settings"></param>
		public SystemSettingsSwitcher(CommandBase owner, SystemSettingsSwitcherSettings settings) {
			// argument checks
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}
			// settings can be null

			// initialize members
			this.Owner = owner;

			bool enabled;
			WebProxy actualProxy;
			if (settings == null) {
				// simple initialization (ex. to restore)
				enabled = true;
				actualProxy = null;
			} else {
				// usual initialization
				enabled = settings.EnableSystemSettingsSwitch;
				if (settings.ActualProxy != null) {
					actualProxy = CreateWebProxy(settings.ActualProxy);
					if (actualProxy != null) {
						if (TestWebProxy(actualProxy) == false) {
							string endPoint = $"{settings.ActualProxy.Host}:{settings.ActualProxy.Port}";
							string message = string.Format(Resources.SystemSettingsSwitcher_Message_ProxyIsNotConnectable, endPoint);
							throw new Exception(message);
						}
					}
				} else {
					actualProxy = DetectSystemProxy();
					// Note that actualProxy may be null
				}
			}

			this.Enabled = enabled;
			this.ActualProxy = actualProxy;

			return;
		}

		#endregion


		#region methods

		public SystemSettings Switch(Proxy proxy) {
			// argument checks
			if (proxy == null) {
				throw new ArgumentNullException(nameof(proxy));
			}

			// state checks
			if (this.Enabled == false) {
				return null;
			}

			// preparations
			SystemSettings switching = GetSwitchingSystemSettings(proxy);
			SystemSettings backup = GetCurrentSystemSettings();

			// switch the system setting
			if (SwitchToInternal(switching, backup, systemSessionEnding: false) == false) {
				// actually, not switched
				// backup is no use
				backup = null;
			}

			return backup;
		}

		public void Restore(SystemSettings backup, bool systemSessionEnding) {
			// argument checks
			if (backup == null) {
				throw new ArgumentNullException(nameof(backup));
			}

			// restore the system setting
			SwitchToInternal(backup, null, systemSessionEnding);

			return;
		}

		public void Restore(IObjectData data, bool systemSessionEnding) {
			// argument checks
			if (data == null) {
				throw new ArgumentNullException(nameof(data));
			}

			// create a new SystemSettings instance
			Restore(CreateSystemSettings(data), systemSessionEnding);
		}

		public SystemSettings GetCurrentSystemSettings() {
			// create a new SystemSettings instance
			SystemSettings settings = CreateSystemSettings(null);

			// set the current system settings into the instance
			SetCurrentSystemSettingsTo(settings);

			return settings;			
		}

		public SystemSettings GetSwitchingSystemSettings(Proxy proxy) {
			// argument checks
			if (proxy == null) {
				throw new ArgumentNullException(nameof(proxy));
			}

			// create a new SystemSettings instance
			SystemSettings settings = CreateSystemSettings(null);

			// set the switching system settings into the instance
			SetSwitchingSystemSettingsTo(settings, proxy);

			return settings;
		}

		public WebProxy DetectSystemProxy() {
			// detect the system web proxy by try to give external urls
			// ToDo: return IWebProxy which can emulate the *.pac file currently effective.
			// Note this implementation simply detect a possible typical proxy.
			// Actual system logic to select proxy may be complicated,
			// for example, it may be scripted by an auto configuration script (*.pac).
			// If WebRequest.GetSystemWebProxy() returns IWebProxy of fixed logic at this point,
			// it can be returned simply here.
			// But this IWebProxy instance will reflect upcoming system proxy switch.
			// So this implementation returns fixed address IWebProxy.
			IWebProxy proxy = WebRequest.GetSystemWebProxy();
			Func<string, WebProxy> detect = (sampleExternalUrl) => {
				Uri sampleUri = new Uri(sampleExternalUrl);
				WebProxy value = null;
				if (proxy.IsBypassed(sampleUri) == false) {
					Uri uri = proxy.GetProxy(sampleUri);
					if (uri != sampleUri) {
						// uri seems to be a proxy
						value = new WebProxy(uri.Host, uri.Port);
					}
				}
				return value;
			};

			// try with google's URL
			WebProxy systemProxy = detect("http://www.google.com/");
			if (systemProxy == null) {
				// try with Microsoft's URL
				systemProxy = detect("http://www.microsoft.com/");
			}

			return systemProxy; // may be null
		}

		protected static string GetAppSettings(string key) {
			string value = ConfigurationManager.AppSettings[key];
			if (string.IsNullOrWhiteSpace(value)) {
				value = null;
			}

			return value;
		}

		public static string GetDefaultActualProxyHostName() {
			return GetAppSettings(ConfigNames.DefaultActualProxyHostName);
		}

		public static int? GetDefaultActualProxyPort() {
			int? value = null;
			try {
				string configValue = ConfigurationManager.AppSettings[ConfigNames.DefaultActualProxyPort];
				if (string.IsNullOrEmpty(configValue) == false) {
					value = int.Parse(configValue);
				}
			} catch {
				Debug.Assert(value == null);
				// continue
			}

			return value;
		}

		public static string GetTestUrl() {
			string value = GetAppSettings(ConfigNames.TestUrl);
			if (value == null) {
				// use Microsoft's test page as default value
				// This url is the one which Windows uses to check Internet connectivity.
				// See https://technet.microsoft.com/en-us/library/bc3bf74c-9b46-4258-9d3e-3ed159199df8 for details.
				value = "http://www.msftncsi.com/ncsi.txt";
			}

			return value;
		}

		#endregion


		#region overridables

		protected virtual WebProxy CreateWebProxy(ActualProxySettings settings) {
			// argument checks
			Debug.Assert(settings != null);
			Debug.Assert(string.IsNullOrEmpty(settings.Host) == false);

			// create a WebProxy object
			return new WebProxy(settings.Host, settings.Port);
		}

		protected virtual bool TestWebProxy(IWebProxy proxy) {
			bool result = true;
			try {
				// get test url specified in application config file
				// (not in the settings file because this information is supposed to be set for site)
				string targetUrl = SystemSettingsSwitcher.GetTestUrl();
				Debug.Assert(string.IsNullOrEmpty(targetUrl) == false);

				WebClientForTest webClient = new WebClientForTest();
				webClient.Proxy = proxy;
				webClient.DownloadData(targetUrl);  // an exception is thrown on error
				this.Owner.LogVerbose("ActualProxy check: OK");
			} catch (WebException exception) {
				// check only proxy related error
				// Note that exception.Status is WebExceptionStatus.ProtocolError (503) for unknown targetUrl.
				if (exception.Status != WebExceptionStatus.ProtocolError) {
					result = false;
				}
				if (result) {
					this.Owner.LogVerbose($"ActualProxy check: {exception.Status} -> OK");
				} else {
					this.Owner.LogError($"ActualProxy check: {exception.Status} -> NG");
				}
			}

			return result;
		}

		protected virtual SystemSettings CreateSystemSettings(IObjectData data) {
			return new SystemSettings(data);
		}

		protected virtual void SetCurrentSystemSettingsTo(SystemSettings settings) {
			return;
		}

		protected virtual void SetSwitchingSystemSettingsTo(SystemSettings settings, Proxy proxy) {
			return;
		}

		protected virtual bool SwitchTo(SystemSettings settings, SystemSettings backup) {
			// argument checks
			// backup can be null

			return false;   // not switched, by default
		}

		protected virtual void NotifySwitched(bool systemSessionEnding) {
			return;
		}

		#endregion


		#region privates

		private bool SwitchToInternal(SystemSettings settings, SystemSettings backup, bool systemSessionEnding) {
			bool switched = false;

			// switch the system settings
			try {
				switched = SwitchTo(settings, backup);
			} catch {
				if (backup != null) {
					try {
						SwitchTo(backup, null);
					} catch (Exception exception) {
						this.Owner.ShowRestoreSystemSettingsErrorMessage(exception.Message);
						// continue
					}
				}
				throw;
			}

			// notify the system settings change
			if (switched) {
				try {
					NotifySwitched(systemSessionEnding);
				} catch (Exception exception) {
					this.Owner.LogVerbose($"Error on notifying system settings switch: {exception.Message}");
					// not fatal, continue
				}
			}

			return switched;
		}

		#endregion
	}
}
