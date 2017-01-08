using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;


namespace MAPE.Windows.GUI {
	public partial class NotifyIconComponent: Component {
		#region creation and disposal

		public NotifyIconComponent() {
			InitializeComponent();
		}

		public NotifyIconComponent(IContainer container) {
			container.Add(this);

			InitializeComponent();
		}

		#endregion
	}
}
