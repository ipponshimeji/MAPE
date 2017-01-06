using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using MAPE.Utils;
using MAPE.Server;


namespace MAPE.Command {
	public static class RunningProxyStateSettingsExtensions {
		#region methods

		public static WebProxy GetWebProxyValue(this Settings settings, string settingName, WebProxy defaultValue) {
			Settings.Value value = settings.GetValue(settingName);
			if (value.IsNull == false) {
				return value.GetObjectValue().CreateWebProxy();
			} else {
				return defaultValue;
			}
		}

		public static WebProxy CreateWebProxy(this Settings settings) {
			Settings.Value host = settings.GetValue(RunningProxyState.SettingNames.Host);
			Settings.Value port = settings.GetValue(RunningProxyState.SettingNames.Port);
			if (host.IsNull || port.IsNull) {
				throw new FormatException($"Both '{RunningProxyState.SettingNames.Host}' and '{RunningProxyState.SettingNames.Port}' settings are indispensable.");
			}

			return new WebProxy(host.GetStringValue(), port.GetInt32Value());
		}

		public static void SetWebProxyValue(this Settings settings, string settingName, WebProxy value, bool omitDefault = false, WebProxy defaultValue = null) {
			// argument checks
			if (settingName == null) {
				throw new ArgumentNullException(nameof(settingName));
			}

			// add a setting if necessary 
			if (omitDefault == false || value != defaultValue) {
				settings.SetObjectValue(settingName, GetWebProxySettings(value, omitDefault));
			}

			return;
		}

		public static Settings GetWebProxySettings(WebProxy value, bool omitDefault) {
			// argument checks
			if (value == null) {
				return Settings.NullSettings;
			}

			// create settings of the DnsEndPoint
			Settings settings = Settings.CreateEmptySettings();
			Uri address = value.Address;

			settings.SetStringValue(RunningProxyState.SettingNames.Host, address.Host);
			settings.SetInt32Value(RunningProxyState.SettingNames.Port, address.Port);

			return settings;
		}

		#endregion
	}

	public class RunningProxyState: IDisposable {
		#region types

		public static class SettingNames {
			#region constants

			public const string ActualProxy = "ActualProxy";

			public const string Host = "Host";

			public const string Port = "Port";

			#endregion
		}

		#endregion


		#region data

		protected readonly CommandBase Owner;

		protected Proxy Proxy { get; private set; }

		protected bool SystemSettingsSwithed { get; private set; }

		#endregion


		#region creation and disposal

		public RunningProxyState(CommandBase owner) {
			// argument checks
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}

			// inirialize members
			this.Owner = owner;
			this.Proxy = null;
			this.SystemSettingsSwithed = false;

			return;
		}

		public virtual void Dispose() {
			if (this.Proxy != null) {
				Stop();
			}

			return;
		}

		#endregion


		#region methods

		public void Start(Settings settings, Settings proxySettings, IProxyRunner proxyRunner) {
			// argument checks
			// settings can contain null
			// proxySettings can contain null
			if (proxyRunner == null) {
				throw new ArgumentNullException(nameof(proxyRunner));
			}

			// read settings
			WebProxy actualProxy = settings.GetWebProxyValue(SettingNames.ActualProxy, null);
			if (actualProxy == null) {
				actualProxy = DetectSystemProxy();
			}

			// create a proxy
			Proxy proxy = this.Owner.ComponentFactory.CreateProxy(proxySettings);
			proxy.ActualProxy = actualProxy;
			proxy.KeepServerCredential = (this.Owner.CredentialPersistence != CredentialPersistence.Session);
			proxy.Start(proxyRunner);
			this.Proxy = proxy;

			// switch system settings
			this.SystemSettingsSwithed = SwitchSystemSettings();

			return;
		}

		public bool Stop(int millisecondsTimeout = 0) {
			// restore the system settings
			if (this.SystemSettingsSwithed) {
				try {
					RestoreSystemSettings();
				} catch (Exception exception) {
					// ToDo: the way to send the message to owner
//					Console.Error.Write($"Fail to restore the previous system settings: {exception.Message}");
//					Console.Error.Write("Please restore it manually.");
					// continue
				}
				this.SystemSettingsSwithed = false;
			}

			// stop and dispose the proxy 
			Proxy proxy = this.Proxy;
			this.Proxy = null;
			bool stopConfirmed = true;
			if (proxy != null) {
				stopConfirmed = proxy.Stop(millisecondsTimeout);
				proxy.Dispose();
			}

			return stopConfirmed;
		}

		#endregion


		#region overridables

		protected virtual bool SwitchSystemSettings() {
			// by default, does not switch the system settings
			return false;	// not swithed
		}

		protected virtual void RestoreSystemSettings() {
			// by default, do nothing
		}

		protected virtual WebProxy DetectSystemProxy() {
			// detect the system web proxy by try to give external urls
			IWebProxy proxy = WebRequest.GetSystemWebProxy();
			Func<string, DnsEndPoint> detect = (sampleExternalUrl) => {
				Uri sampleUri = new Uri(sampleExternalUrl);
				DnsEndPoint value = null;
				if (proxy.IsBypassed(sampleUri) == false) {
					Uri uri = proxy.GetProxy(sampleUri);
					if (uri != sampleUri) {
						// uri seems to be a proxy
						value = new DnsEndPoint(uri.Host, uri.Port);
					}
				}
				return value;
			};

			// try with google's URL
			DnsEndPoint endPoint = detect("http://www.google.com/");
			if (endPoint == null) {
				// try with Microsoft's URL
				endPoint = detect("http://www.microsoft.com/");
			}

			return (endPoint == null)? null: new WebProxy(endPoint.Host, endPoint.Port);
		}

		#endregion
	}
}
