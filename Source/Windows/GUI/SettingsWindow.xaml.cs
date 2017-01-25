using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MAPE.Windows.GUI {
	/// <summary>
	/// SettingsWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class SettingsWindow: Window {
		#region creation and disposal

		public SettingsWindow() {
			InitializeComponent();
		}

		#endregion


		#region overrides

		protected override void OnInitialized(EventArgs e) {
			// initialize the base class level
			base.OnInitialized(e);

			// initialize this class level
			this.Icon = App.Current.OnIcon;
		}

		#endregion
	}
}
