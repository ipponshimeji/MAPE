using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;


namespace MAPE.Windows.GUI {
	/// <summary>
	/// App.xaml の相互作用ロジック
	/// </summary>
	public partial class App: Application {
		private NotifyIconComponent notifyIcon;

		protected override void OnStartup(StartupEventArgs e) {
			base.OnStartup(e);
			this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			this.notifyIcon = new NotifyIconComponent();
		}

		protected override void OnExit(ExitEventArgs e) {
			this.notifyIcon.Dispose();
			base.OnExit(e);
		}
	}
}
