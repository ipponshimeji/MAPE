using System;
using System.Net;
using Microsoft.Win32;
using MAPE.Utils;
using MAPE.Command;
using MAPE.Server;


namespace MAPE.Windows {
	public class RunningProxyStateForWindows: RunningProxyState {
		#region data

		private WebProxy proxySettings;

		#endregion


		#region creation and disposal

		public RunningProxyStateForWindows(CommandBase owner): base(owner) {
		}

		#endregion


		#region overrides

		protected override bool SwitchSystemSettings() {
			// ToDo: implement
			// Internet Settings
			// ProxyEnable
			// ProxyServer

			// Environment Variables
			// http_proxy
			// https_proxy

			return false;   // not swithed
		}

		protected override void RestoreSystemSettings() {
			// ToDo: implement
		}

		#endregion


		#region private

		private RegistryKey GetInternetSettingKey() {
			return Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings");
		}

		#endregion
	}
}
