using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;


namespace MAPE.Windows.GUI {
	public partial class NotifyIconComponent: Component {
		#region creation and disposal

		public NotifyIconComponent() {
			InitializeComponent();
			InitializeMisc();
		}

		public NotifyIconComponent(IContainer container) {
			container.Add(this);

			InitializeComponent();
			InitializeMisc();
		}

		#endregion


		#region privates

		private void InitializeMisc() {
			this.notifyIcon.Text = Properties.Resources.App_Title;
		}

		#endregion
	}
}
