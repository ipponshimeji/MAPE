using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MAPE.Windows.Settings;


namespace MAPE.Windows.GUI {
	public partial class SystemSettingsSwitchSettingsControl: UserControl {
		#region data

		private SystemSettingsSwitcherForWindowsSettings systemSettingsSwitcherSettings = null;

		private bool suppressUpdatingBypassLocalSource = false;

		#endregion


		#region properties

		public SystemSettingsSwitcherForWindowsSettings SystemSettingsSwitcherSettings {
			get {
				return this.systemSettingsSwitcherSettings;
			}
			set {
				this.systemSettingsSwitcherSettings = value;

				// update UI
				this.enableSystemSettingSwitchCheckBox.GetBindingExpression(CheckBox.IsCheckedProperty)?.UpdateTarget();
				this.exclusionTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
				SetBypassLocalSuppressingUpdatingSource(value.BypassLocal);
			}
		}

		#endregion


		#region properties - data binding adapters

		public bool EnableSystemSettingsSwitch {
			get {
				SystemSettingsSwitcherForWindowsSettings settings = this.SystemSettingsSwitcherSettings;
				return (settings == null)? false: settings.EnableSystemSettingsSwitch;
			}
			set {
				SystemSettingsSwitcherForWindowsSettings settings = this.SystemSettingsSwitcherSettings;
				if (settings != null) {
					settings.EnableSystemSettingsSwitch = value;
				}
			}
		}

		public string ProxyOverride {
			get {
				SystemSettingsSwitcherForWindowsSettings settings = this.SystemSettingsSwitcherSettings;
				return (settings == null)? string.Empty: settings.FilteredProxyOverride;
			}
			set {
				SystemSettingsSwitcherForWindowsSettings settings = this.SystemSettingsSwitcherSettings;
				if (settings != null) {
					settings.FilteredProxyOverride = value;
					SetBypassLocalSuppressingUpdatingSource(settings.BypassLocal);
				}
			}
		}

		#endregion


		#region creation and disposal

		public SystemSettingsSwitchSettingsControl() {
			// initialize components
			InitializeComponent();
			this.DataContext = this;

			return;
		}

		#endregion


		#region methods

		public Control GetErrorControl() {
			// check error state of controls
			if (Validation.GetHasError(this.enableSystemSettingSwitchCheckBox)) {
				return this.enableSystemSettingSwitchCheckBox;
			}
			if (Validation.GetHasError(this.exclusionTextBox)) {
				return this.exclusionTextBox;
			}

			return null;
		}

		#endregion


		#region privates

		private void SetBypassLocalSuppressingUpdatingSource(bool value) {
			// state checks
			Debug.Assert(this.suppressUpdatingBypassLocalSource == false);

			// change this.bypassProxyCheckBox.IsChecked supressing updating ProxyOverride
			this.suppressUpdatingBypassLocalSource = true;
			try {
				this.bypassLocalCheckBox.IsChecked = value;
			} finally {
				this.suppressUpdatingBypassLocalSource = false;
			}

			return;
		}

		#endregion


		#region event handlers

		private void bypassLocalCheckBox_Checked(object sender, RoutedEventArgs e) {
			if (this.suppressUpdatingBypassLocalSource == false) {
				this.SystemSettingsSwitcherSettings.BypassLocal = true;
			}
		}

		private void bypassLocalCheckBox_Unchecked(object sender, RoutedEventArgs e) {
			if (this.suppressUpdatingBypassLocalSource == false) {
				this.SystemSettingsSwitcherSettings.BypassLocal = false;
			}
		}

		#endregion
	}
}
