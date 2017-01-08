using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;


namespace MAPE.Windows.GUI {
	public partial class NotifyIconComponent: Component {
		public NotifyIconComponent() {
			InitializeComponent();

			this.startMenuItem.Click += this.startMenuItem_Click;
			this.stopMenuItem.Click += this.stopMenuItem_Click;
			this.openMenuItem.Click += this.openMenuItem_Click;
			this.exitMenuItem.Click += this.exitMenuItem_Click;
		}

		public NotifyIconComponent(IContainer container) {
			container.Add(this);

			InitializeComponent();
		}


		#region event handlers

		private void startMenuItem_Click(object sender, EventArgs e) {
		}

		private void stopMenuItem_Click(object sender, EventArgs e) {
		}

		private void openMenuItem_Click(object sender, EventArgs e) {
			var wnd = new MainWindow();
			wnd.Show();
		}

		private void exitMenuItem_Click(object sender, EventArgs e) {
			Application.Current.Shutdown();
		}

		#endregion
	}
}
