using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using MAPE.Utils;
using MAPE.Server;


namespace MAPE.Command {
	public class RunningProxyState: IDisposable {
		#region data

		protected readonly CommandBase Owner;

		protected Proxy Proxy { get; private set; }

		protected bool SystemSettingsSwithed { get; private set; }

		#endregion


		#region creation and disposal

		public RunningProxyState(CommandBase owner, Settings settings) {
			// argument checks
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}
			// settings can be null

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

		public void Start(Settings proxySettings, IProxyRunner proxyRunner) {
			// argument checks
			if (proxyRunner == null) {
				throw new ArgumentNullException(nameof(proxyRunner));
			}

			// create a proxy
			Proxy proxy = this.Owner.ComponentFactory.CreateProxy(proxySettings);
			if (proxy.Server == null) {
				// if Server is not specified, give it the current system proxy
				proxy.Server = DetectSystemProxy();
			}
			proxy.Start(proxyRunner);

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

		protected virtual DnsEndPoint DetectSystemProxy() {
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

			return endPoint;
		}

		#endregion
	}
}
