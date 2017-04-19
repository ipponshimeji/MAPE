using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MAPE.Command.Settings;


namespace MAPE.Windows.GUI {
	public partial class ActualProxySettingsControl: UserControl {
		#region data

		private SystemSettingsSwitcherSettings systemSettingsSwitcherSettings = null;

		public string DefaultActualProxyHostName { get; set; } = null;

		public int? DefaultActualProxyPort { get; set; } = null;

		#endregion


		#region properties

		public SystemSettingsSwitcherSettings SystemSettingsSwitcherSettings {
			get {
				return this.systemSettingsSwitcherSettings;
			}
			set {
				// adjust the value
				if (this.AutoDetectEnabled == false && value.ActualProxy == null) {
					// user must specify actuao proxy information
					value.ActualProxy = CreateActualProxySettings();
				}

				// set the value
				this.systemSettingsSwitcherSettings = value;

				// update UI
				this.autoDetectProxyCheckBox.IsChecked = (value.ActualProxy == null);
				// HostName and Port are updated via event fired by the line above
			}
		}

		public bool AutoDetectEnabled {
			get {
				return this.autoDetectProxyCheckBox.IsEnabled;
			}
			set {
				if (value == false) {
					this.autoDetectProxyCheckBox.IsChecked = false;
				}
				this.autoDetectProxyCheckBox.IsEnabled = value;
			}
		}

		private bool AutoDetectProxy {
			get {
				return this.autoDetectProxyCheckBox.IsChecked ?? false;
			}
		}

		#endregion


		#region properties - data binding adapters

		public string HostName {
			get {
				ActualProxySettings actualProxySettings = this.systemSettingsSwitcherSettings.ActualProxy;
				return (actualProxySettings == null)? string.Empty: actualProxySettings.Host;
			}
			set {
				ActualProxySettings actualProxySettings = this.SystemSettingsSwitcherSettings.ActualProxy;
				if (actualProxySettings == null) {
					return;	// ignore
				}

				actualProxySettings.Host = value;
			}
		}

		public int Port {
			get {
				ActualProxySettings actualProxySettings = this.SystemSettingsSwitcherSettings.ActualProxy;
				return (actualProxySettings == null)? ActualProxySettings.Defaults.Port: actualProxySettings.Port;
			}
			set {
				ActualProxySettings actualProxySettings = this.SystemSettingsSwitcherSettings.ActualProxy;
				if (actualProxySettings == null) {
					// ignore
					return;
				}

				actualProxySettings.Port = value;
			}
		}

		#endregion


		#region creation and disposal

		public ActualProxySettingsControl() {
			// initialize components
			InitializeComponent();
			this.DataContext = this;

			return;
		}

		#endregion


		#region methods

		public Control GetErrorControl(bool setupMode = false) {
			// check error state of controls
			if (Validation.GetHasError(this.hostNameTextBox)) {
				return this.hostNameTextBox;
			}
			if (Validation.GetHasError(this.portTextBox)) {
				return this.portTextBox;
			}

			if (setupMode) {
				if (this.AutoDetectProxy == false && ActualProxySettings.Defaults.IsDefaultHostName(this.HostName)) {
					// HostName is not edited
					return this.hostNameTextBox;
				}
			}

			return null;
		}

		#endregion


		#region privates

		private void UpdateTextSource(TextBox textBox) {
			// update the source
			textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
		}

		private void ClearTextError(TextBox textBox) {
			// clear errors on property binding
			BindingExpression binding = textBox.GetBindingExpression(TextBox.TextProperty);
			if (binding != null) {
				Validation.ClearInvalid(binding);
			}

			return;
		}

		private ActualProxySettings CreateActualProxySettings() {
			ActualProxySettings actualProxySettings = new ActualProxySettings();
			if (string.IsNullOrEmpty(this.DefaultActualProxyHostName) == false) {
				actualProxySettings.Host = this.DefaultActualProxyHostName;
			}
			if (this.DefaultActualProxyPort != null) {
				actualProxySettings.Port = this.DefaultActualProxyPort.Value;
			}

			return actualProxySettings;
		}

		#endregion


		#region event handlers

		private void autoDetectProxyCheckBox_Checked(object sender, RoutedEventArgs e) {
			// clear ActualProxy object
			this.SystemSettingsSwitcherSettings.ActualProxy = null;

			// disable the settings for ActualProxy
			this.hostNameTextBox.IsEnabled = false;
			this.portTextBox.IsEnabled = false;

			// clear errors for ActualProxy
			ClearTextError(this.hostNameTextBox);
			ClearTextError(this.portTextBox);

			return;
		}

		private void autoDetectProxyCheckBox_Unchecked(object sender, RoutedEventArgs e) {
			// set ActualProxy object
			this.SystemSettingsSwitcherSettings.ActualProxy = CreateActualProxySettings();

			// enable the settings for ActualProxy
			this.hostNameTextBox.IsEnabled = true;
			this.portTextBox.IsEnabled = true;

			// update the bindings
			UpdateTextSource(this.hostNameTextBox);
			UpdateTextSource(this.portTextBox);

			return;
		}

		#endregion
	}
}
