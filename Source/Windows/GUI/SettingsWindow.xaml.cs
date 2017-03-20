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
using MAPE.Utils;
using MAPE.Command;
using MAPE.Command.Settings;
using MAPE.Windows.GUI.Settings;


namespace MAPE.Windows.GUI {
	/// <summary>
	/// SettingsWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class SettingsWindow: Window {
		#region data

		public CommandForWindowsGUISettings Settings {
			get; private set;
		}

		public bool SaveAsDefault {
			get; private set;
		}

		#endregion


		#region creation and disposal

		internal SettingsWindow(CommandForWindowsGUISettings settings, bool enableSaveAsDefault) {
			// argument checks
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}

			// initialize members
			this.Settings = settings;
			this.SaveAsDefault = false;

			// initialize components
			InitializeComponent();
			this.saveAsDefaultButton.IsEnabled = enableSaveAsDefault;

			return;
		}

		#endregion


		#region overrides

		protected override void OnInitialized(EventArgs e) {
			// initialize the base class level
			base.OnInitialized(e);

			// Root
			CommandSettings commandSettings = this.Settings;

			// SystemSettingsSwitcher
			SystemSettingsSwitcherSettings systemSettingsSwitcherSettings = commandSettings.SystemSettingsSwitcher;
			ActualProxySettings actualProxy = systemSettingsSwitcherSettings.ActualProxy;
			if (actualProxy == null) {
				this.autoDetectProxyCheckBox.IsChecked = true;
				this.hostNameTextBox.Text = "";
				this.portTextBox.Text = "";
			} else {
				this.autoDetectProxyCheckBox.IsChecked = false;
				this.hostNameTextBox.Text = actualProxy.Host;
				this.portTextBox.Text = actualProxy.Port.ToString();
			}

			// Proxy
			// Credential
		}

		#endregion
	}
}
