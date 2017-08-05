using System;
using System.Collections.Generic;
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

            public const string ProxyTestUrl = "ProxyTestUrl";

            public const string TestUrl = "TestUrl";

			#endregion
		}

		#endregion


		#region data

		public readonly CommandBase Owner;

		public bool Enabled { get; protected set; } = true;

		public DnsEndPoint ActualProxyEndPoint { get; protected set; } = null;

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
			DnsEndPoint actualProxyEndPoint;
			if (settings == null) {
				// simple initialization (ex. to restore only)
				enabled = true;
				actualProxyEndPoint = null;
			} else {
				// usual initialization
				enabled = settings.EnableSystemSettingsSwitch;
				ActualProxySettings actualProxySettings = settings.ActualProxy;
				if (actualProxySettings != null) {
					actualProxyEndPoint = new DnsEndPoint(actualProxySettings.Host, actualProxySettings.Port);
				} else {
					actualProxyEndPoint = null;
				}
            }

            this.Enabled = enabled;
			this.ActualProxyEndPoint = actualProxyEndPoint;

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

		public IActualProxy GetActualProxy(SystemSettings systemSettings = null) {
			// argument checks
			// systemSettings can be null

			if (this.ActualProxyEndPoint != null) {
				// actual proxy is explicitly specified by the settings 
				return new StaticActualProxy(this.ActualProxyEndPoint);
			} else {
				// detect system settings
				if (systemSettings == null) {
					systemSettings = GetCurrentSystemSettings();
				}
				return GetSystemActualProxy(systemSettings);
			}
		}

		public IActualProxy DetectSystemActualProxy(SystemSettings systemSettings = null) {
			// argument checks
			if (systemSettings == null) {
				systemSettings = GetCurrentSystemSettings();
			}

			return GetSystemActualProxy(systemSettings);
		}

		protected static string GetAppSettings(string key, string defaultValue = null) {
			string value = ConfigurationManager.AppSettings[key];
			if (value == null) {
				value = defaultValue;
			}

			return value;
		}

		public static string GetDefaultActualProxyHostName() {
			return Util.Trim(GetAppSettings(ConfigNames.DefaultActualProxyHostName));
		}

		public static int? GetDefaultActualProxyPort() {
			int? value = null;
			try {
				string configValue = GetAppSettings(ConfigNames.DefaultActualProxyPort);
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
			if (string.IsNullOrWhiteSpace(value)) {
                value = GetDefaultTestUrl();
			}

			return Util.Trim(value);
		}

        public static string GetProxyTestUrl() {
            string value = GetAppSettings(ConfigNames.ProxyTestUrl);
            if (string.IsNullOrWhiteSpace(value)) {
                value = GetDefaultTestUrl();
            }

            return Util.Trim(value);
        }

        private static string GetDefaultTestUrl() {
            // use Microsoft's test page as default value
            // This url is the one which Windows uses to check Internet connectivity.
            // See https://technet.microsoft.com/en-us/library/bc3bf74c-9b46-4258-9d3e-3ed159199df8 for details.
            return "http://www.msftncsi.com/ncsi.txt";
        }

		#endregion


		#region overridables

		protected virtual IActualProxy GetSystemActualProxy(SystemSettings systemSettings) {
			// argument checks
			Debug.Assert(systemSettings != null);

			// detect the system web proxy by try to give external urls
			// Note this implementation simply detect a possible typical proxy.
			IWebProxy systemProxy = WebRequest.GetSystemWebProxy();
			Func<string, IActualProxy> detect = (sampleExternalUrl) => {
				Uri sampleUri = new Uri(sampleExternalUrl);
				IActualProxy value = null;
				if (systemProxy.IsBypassed(sampleUri) == false) {
					Uri uri = systemProxy.GetProxy(sampleUri);
					if (uri != sampleUri) {
						// uri seems to be a proxy
						value = new StaticActualProxy(new DnsEndPoint(uri.Host, uri.Port));
					}
				}
				return value;
			};

			// try with google's URL
			IActualProxy actualProxy = detect("http://www.google.com/");
			if (actualProxy == null) {
				// try with Microsoft's URL
				actualProxy = detect("http://www.microsoft.com/");
			}

			return actualProxy; // note that it may be null
		}

		public virtual bool TestWebProxy(IActualProxy actualProxy) {
			// get test url specified in application config file
			// (not in the settings file because this information is supposed to be set for site)
			string targetUrl = SystemSettingsSwitcher.GetProxyTestUrl();
			Debug.Assert(string.IsNullOrEmpty(targetUrl) == false);
			Uri target = new Uri(targetUrl);
			DnsEndPoint targetEndPoint = new DnsEndPoint(target.Host, target.Port);

			WebClientForTest webClient = new WebClientForTest();
            webClient.Timeout = 10 * 1000;      // 10 seconds

			// test each proxy candidate
			CommandBase owner = this.Owner;
			bool result = false;
			IReadOnlyCollection<DnsEndPoint> endPoints = actualProxy.GetProxyEndPoints(target);
			if (endPoints != null) {
				foreach (DnsEndPoint endPoint in endPoints) {
					owner.LogVerbose($"ActualProxy check: to {endPoint.Host}:{endPoint.Port}");
					try {
						webClient.Proxy = new WebProxy(endPoint.Host, endPoint.Port);
						webClient.DownloadData(targetUrl);  // an exception is thrown on error
						result = true;
					} catch (WebException exception) {
						// Note that a protocol error indicates that the end point exists
						result = (exception.Status == WebExceptionStatus.ProtocolError);
						if (result) {
							owner.LogVerbose($"ActualProxy check: {exception.Status} -> OK");
						} else {
							owner.LogError($"ActualProxy check: {exception.Status} -> NG");
						}
					}
					if (result) {
						// a valid proxy is found
						break;
					}
				}
			}
			if (result) {
				owner.LogVerbose("ActualProxy check: OK - there is a valid actual proxy.");
			} else {
				owner.LogError("ActualProxy check: NG - no valid proxy.");
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
