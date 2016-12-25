using System;
using Microsoft.Win32;
using MAPE.Command;
using MAPE.Server;


namespace MAPE.Windows {
    public class CLICommandForWindows: CLICommandBase {
		#region types

		public class SystemSettingSwitcher: SystemSettingSwitcherBase {
			#region data
			#endregion


			#region creation and disposal

			public SystemSettingSwitcher(Proxy proxy): base(proxy) {
			}

			#endregion


			#region overrides

			public override bool Switch() {
				// ToDo: implement
				// Internet Settings
				// ProxyEnable
				// ProxyServer

				// Environment Variables
				// http_proxy
				// https_proxy


				return false;   // not switched
			}

			public override void Restore() {
				// ToDo: implement
				return;
			}

			#endregion


			#region private

			private RegistryKey GetInternetSettingKey() {
				return Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings");
			}

			#endregion
		}

		#endregion


		#region creation and disposal

		public CLICommandForWindows(ComponentFactoryForWindows componentFactory): base(componentFactory) {
			return;
		}

		#endregion
	}
}
