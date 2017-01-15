using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using MAPE.Windows.GUI.Properties;


namespace MAPE.Windows.GUI {
	public partial class NotifyIconComponent: Component {
		#region properties

		public Icon Icon {
			get {
				return this.notifyIcon.Icon;
			}
			set {
				this.notifyIcon.Icon = value;
			}
		}

		#endregion


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
			this.notifyIcon.Text = Resources.App_Title;
			this.notifyIcon.Icon = Resources.OffIcon;
		}

		#endregion
	}
}
