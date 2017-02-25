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


namespace MAPE.Windows.GUI {
	/// <summary>
	/// SettingsWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class SettingsWindow: Window {
		#region data

		public Settings Settings {
			get; private set;
		}

		public bool SaveAsDefault {
			get; private set;
		}

		#endregion


		#region creation and disposal

		internal SettingsWindow(Settings settings, bool enableSaveAsDefault) {
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
			Settings rootSettings = this.Settings;

			// SystemSettingsSwitcher
			Settings systemSettingsSwitcherSettings = rootSettings.GetSystemSettingSwitcherSettings();
			Settings.Value value = systemSettingsSwitcherSettings.GetValue(SystemSettingsSwitcher.SettingNames.ActualProxy);
			if (value.IsNull == false) {
				this.autoDetectProxyCheckBox.IsChecked = false;
				Settings actualProxySettings = value.GetObjectValue();
				this.hostNameTextBox.Text = actualProxySettings.GetStringValue(SystemSettingsSwitcher.SettingNames.Host, string.Empty);
				this.portTextBox.Text = "";
			} else {
				this.autoDetectProxyCheckBox.IsChecked = true;
				this.hostNameTextBox.Text = "";
				this.portTextBox.Text = "";
			}

			// Proxy
			// Credential
		}

		#endregion
	}
}
