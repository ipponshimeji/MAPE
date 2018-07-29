using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MAPE.Command.Settings;


namespace MAPE.Windows.GUI {
	public partial class ActualProxySettingsControl: UserControl {
		#region types

		protected class ViewModel {
			#region data

			public SystemSettingsSwitcherSettings SystemSettingsSwitcherSettings { get; set; } = null;

			private ActualProxySettings actualProxySettingsBackup = null;

			#endregion


			#region properties

			protected ActualProxySettings ActualProxySettings {
				get {
					return this.SystemSettingsSwitcherSettings?.ActualProxy;
				}
				set {
					// state checks
					SystemSettingsSwitcherSettings systemSettingsSwitcherSettings = this.SystemSettingsSwitcherSettings;
					if (systemSettingsSwitcherSettings == null) {
						// ignore
						return;
					}

					systemSettingsSwitcherSettings.ActualProxy = value;
				}
			}

			protected ActualProxySettings BindingActualProxySettings {
				get {
					ActualProxySettings value = this.ActualProxySettings;
					if (value == null) {
						// backup may be keeping the last status
						value = this.actualProxySettingsBackup;
					}

					return value;
				}
			}

			#endregion


			#region events

			public event EventHandler HostNameSet = null;

			public event EventHandler ConfigurationScriptSet = null;

			#endregion


			#region properties - binding sources

			public bool AutoDetectEnabled {
				get {
					return this.ActualProxySettings == null;
				}
				set {
					if (value) {
						// enable auto detect (i.e. no ActualProxySettings exists)
						ActualProxySettings actualProxySettings = this.ActualProxySettings;
						if (actualProxySettings != null) {
							this.ActualProxySettings = null;
							this.actualProxySettingsBackup = actualProxySettings;
						}
					} else {
						// disable auto detect (i.e. ActualProxySettings exists)
						ActualProxySettings actualProxySettings = this.ActualProxySettings;
						if (actualProxySettings == null) {
							actualProxySettings = this.actualProxySettingsBackup;
							if (actualProxySettings == null) {
								// create a new ActualProxySettings
								actualProxySettings = CreateActualProxySettings();
							}
							this.ActualProxySettings = actualProxySettings;
						}
					}
				}
			}

			public string HostName {
				get {
					ActualProxySettings actualProxySettings = this.BindingActualProxySettings;
					return (actualProxySettings == null) ? string.Empty : actualProxySettings.Host;
				}
				set {
					// source checks
					ActualProxySettings actualProxySettings = this.BindingActualProxySettings;
					if (actualProxySettings == null) {
						// ignore
						return;
					}

					// argument checks
					if (value != null) {
						value = value.Trim();
					}

					// set the proeprty
					actualProxySettings.Host = value;

					// fire event
					if (this.HostNameSet != null) {
						this.HostNameSet(this, EventArgs.Empty);
					}
				}
			}

			public int Port {
				get {
					// source checks
					ActualProxySettings actualProxySettings = this.BindingActualProxySettings;
					return (actualProxySettings == null) ? ActualProxySettings.Defaults.Port : actualProxySettings.Port;
				}
				set {
					// source checks
					ActualProxySettings actualProxySettings = this.BindingActualProxySettings;
					if (actualProxySettings == null) {
						// ignore
						return;
					}

					actualProxySettings.Port = value;
				}
			}

			public string ConfigurationScript {
				get {
					ActualProxySettings actualProxySettings = this.BindingActualProxySettings;
					return (actualProxySettings == null) ? string.Empty : actualProxySettings.ConfigurationScript;
				}
				set {
					// source checks
					ActualProxySettings actualProxySettings = this.BindingActualProxySettings;
					if (actualProxySettings == null) {
						// ignore
						return;
					}

					// argument checks
					if (value != null) {
						value = value.Trim();
					}

					// set the property
					actualProxySettings.ConfigurationScript = value;

					// fire event
					if (this.ConfigurationScriptSet != null) {
						this.ConfigurationScriptSet(this, EventArgs.Empty);
					}
				}
			}

			#endregion


			#region overridables

			protected virtual ActualProxySettings CreateActualProxySettings() {
				return new ActualProxySettings();
			}

			#endregion
		}

		#endregion


		#region data

		private ViewModel viewModel;

		#endregion


		#region properties

		public SystemSettingsSwitcherSettings SystemSettingsSwitcherSettings {
			get {
				return this.viewModel.SystemSettingsSwitcherSettings;
			}
			set {
				this.viewModel.SystemSettingsSwitcherSettings = value;
			}
		}

		#endregion


		#region creation and disposal

		public ActualProxySettingsControl() {
			// initialize components
			ViewModel viewModel = CreateViewModel();
			viewModel.HostNameSet += viewModel_HostNameSet;
			viewModel.ConfigurationScriptSet += viewModel_ConfigurationScriptSet;
			this.viewModel = viewModel;

			InitializeComponent();
			this.DataContext = viewModel;

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
			if (Validation.GetHasError(this.configurationScriptTextBox)) {
				return this.configurationScriptTextBox;
			}

			if (setupMode) {
				if (
					this.viewModel.AutoDetectEnabled == false &&
					ActualProxySettings.Defaults.IsDefaultHostName(this.viewModel.HostName) &&
					string.IsNullOrEmpty(this.viewModel.ConfigurationScript)
				) {
					// HostName is not edited
					return this.hostNameTextBox;
				}
			}

			return null;
		}

		protected ViewModel GetViewMode() {
			return this.viewModel;
		}

		#endregion


		#region overridables

		protected virtual ViewModel CreateViewModel() {
			return new ViewModel();
		}

		#endregion


		#region privates

		private void UpdateTextTarget(TextBox textBox) {
			// argument checks
			Debug.Assert(textBox != null);

			// update the source
			textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
		}

		private void UpdateActualProxySettingsTarget() {
			UpdateTextTarget(this.hostNameTextBox);
			UpdateTextTarget(this.portTextBox);
			UpdateTextTarget(this.configurationScriptTextBox);
		}

		private void UpdateTextSource(TextBox textBox) {
			// argument checks
			Debug.Assert(textBox != null);

			// update the source
			textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
		}

		private void ClearTextError(TextBox textBox) {
			// argument checks
			Debug.Assert(textBox != null);

			// clear errors on property binding
			BindingExpression binding = textBox.GetBindingExpression(TextBox.TextProperty);
			if (binding != null) {
				Validation.ClearInvalid(binding);
			}

			return;
		}

		#endregion


		#region event handlers

		private void autoDetectProxyCheckBox_Checked(object sender, RoutedEventArgs e) {
			// disable the settings for ActualProxy
			this.hostNameTextBox.IsEnabled = false;
			this.portTextBox.IsEnabled = false;
			this.configurationScriptTextBox.IsEnabled = false;

			// clear errors for ActualProxy
			ClearTextError(this.hostNameTextBox);
			ClearTextError(this.portTextBox);
			ClearTextError(this.configurationScriptTextBox);

			// update the bindings
			UpdateActualProxySettingsTarget();

			return;
		}

		private void autoDetectProxyCheckBox_Unchecked(object sender, RoutedEventArgs e) {
			// enable the settings for ActualProxy
			this.hostNameTextBox.IsEnabled = true;
			this.portTextBox.IsEnabled = true;
			this.configurationScriptTextBox.IsEnabled = true;

			// update the bindings (a new ActualProxySettings instance may be created)
			UpdateActualProxySettingsTarget();

			return;
		}

		private void viewModel_HostNameSet(object sender, EventArgs e) {
			if (string.IsNullOrEmpty(this.viewModel.HostName) == false) {
				// HostName has a substantial value
				BindingExpression binding = configurationScriptTextBox.GetBindingExpression(TextBox.TextProperty);
				if (binding != null && binding.HasError) {
					// retry to set the ConfigurationScript
					binding.UpdateSource();
				}
			}
		}

		private void viewModel_ConfigurationScriptSet(object sender, EventArgs e) {
			if (string.IsNullOrEmpty(this.viewModel.HostName) == false) {
				// ConfigurationScript has a substantial value
				BindingExpression binding = hostNameTextBox.GetBindingExpression(TextBox.TextProperty);
				if (binding != null && binding.HasError) {
					// retry to set the HostName
					binding.UpdateSource();
				}
			}
		}

		#endregion
	}
}
