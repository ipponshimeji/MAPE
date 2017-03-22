﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using MAPE.Server.Settings;
using MAPE.Command;
using MAPE.Command.Settings;
using MAPE.Windows.Settings;
using MAPE.Windows.GUI.Settings;
using System.ComponentModel;

namespace MAPE.Windows.GUI {
	/// <summary>
	/// SettingsWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class SettingsWindow: Window {
		#region types

		[Flags]
		public enum UIStateFlags {
			OKEnabled = 0x01,
			SaveAsDefaultEnabled = 0x02,
			AddListenerEnabled = 0x04,
			RemoveListenerEnabled = 0x08,
			EditListenerEnabled = 0x10,
			UpListenerEnabled = 0x20,
			DownListenerEnabled = 0x40,
			ResetListenerEnabled = 0x80,
			AddCredentialEnabled = 0x100,
			RemoveCredentialEnabled = 0x200,
			EditCredentialEnabled = 0x400,

			None = 0,
			Invariable = AddListenerEnabled | AddCredentialEnabled,
			InitialState = Invariable | OKEnabled | SaveAsDefaultEnabled,
		}

		#endregion


		#region data

		public CommandForWindowsGUISettings CommandSettings {
			get; private set;
		}

		public bool SaveAsDefault {
			get; private set;
		}

		private readonly bool enableSaveAsDefault;

		private readonly bool runningProxy;

		private readonly Control[] validatableControls;

		private UIStateFlags uiState = UIStateFlags.InitialState;

		private bool suppressUpdatingBypassLocalSource = false;

		#endregion


		#region properties - data binding adapters

		public string HostName {
			get {
				ActualProxySettings actualProxySettings = this.CommandSettings.SystemSettingsSwitcher.ActualProxy;
				return (actualProxySettings == null)? string.Empty: actualProxySettings.Host;
			}
			set {
				ActualProxySettings actualProxySettings = this.CommandSettings.SystemSettingsSwitcher.ActualProxy;
				if (actualProxySettings == null) {
					// ignore
					return;
				}

				actualProxySettings.Host = value;
			}
		}

		public int Port {
			get {
				ActualProxySettings actualProxySettings = this.CommandSettings.SystemSettingsSwitcher.ActualProxy;
				return (actualProxySettings == null)? ActualProxySettings.Defaults.Port: actualProxySettings.Port;
			}
			set {
				ActualProxySettings actualProxySettings = this.CommandSettings.SystemSettingsSwitcher.ActualProxy;
				if (actualProxySettings == null) {
					// ignore
					return;
				}

				actualProxySettings.Port = value;
			}
		}

		public bool EnableSystemSettingsSwitch {
			get {
				return this.CommandSettings.SystemSettingsSwitcher.EnableSystemSettingsSwitch;
			}
			set {
				this.CommandSettings.SystemSettingsSwitcher.EnableSystemSettingsSwitch = value;
			}
		}

		public string ProxyOverride {
			get {
				return this.CommandSettings.SystemSettingsSwitcher.FilteredProxyOverride;
			}
			set {
				SystemSettingsSwitcherForWindowsSettings systemSettingsSwitcherSettings = this.CommandSettings.SystemSettingsSwitcher;
				systemSettingsSwitcherSettings.FilteredProxyOverride = value;
				SetBypassLocalSuppressingUpdatingSource(systemSettingsSwitcherSettings.BypassLocal);
			}
		}

		public int RetryCount {
			get {
				return this.CommandSettings.Proxy.RetryCount;
			}
			set {
				this.CommandSettings.Proxy.RetryCount = value;
			}
		}

		public TraceLevel LogLevel {
			get {
				return this.CommandSettings.LogLevel;
			}
			set {
				this.CommandSettings.LogLevel = value;
			}
		}

		#endregion


		#region creation and disposal

		internal SettingsWindow(CommandForWindowsGUISettings commandSettings, bool enableSaveAsDefault, bool runningProxy) {
			// argument checks
			if (commandSettings == null) {
				throw new ArgumentNullException(nameof(commandSettings));
			}

			// initialize members
			this.CommandSettings = commandSettings;
			this.SaveAsDefault = false;
			this.enableSaveAsDefault = enableSaveAsDefault;
			this.runningProxy = runningProxy;

			// initialize components
			InitializeComponent();
			this.validatableControls = new Control[] {
				this.hostNameTextBox,
				this.portTextBox,
				this.retryTextBox
			};
			this.Icon = App.Current.OnIcon;
			this.DataContext = this;

			// Proxy Tab
			// Actual Proxy
			SystemSettingsSwitcherForWindowsSettings systemSettingsSwitcherSettings = commandSettings.SystemSettingsSwitcher;
			ActualProxySettings actualProxy = systemSettingsSwitcherSettings.ActualProxy;
			this.autoDetectProxyCheckBox.IsChecked = (actualProxy == null);
			// this.hostNameTextBox.Text is bound to this.HostName
			// this.portTextBox.Text is bound to this.Port

			// SystemSettingSwither
			// this.enableSystemSettingSwitherCheckBox.IsChecked is bound to this.EnableSystemSettingSwitch
			SetBypassLocalSuppressingUpdatingSource(systemSettingsSwitcherSettings.BypassLocal);
			// this.exclusionTextBox.Text is bound to this.ProxyOverride

			// Misc
			// this.retryTextBox.Text is bound to this.RetryCount

			// Listener Tab
			ProxySettings proxySettings = commandSettings.Proxy;
			ItemCollection items = this.listenerListView.Items;
			items.Add(proxySettings.MainListener);
			if (proxySettings.AdditionalListeners != null) {
				foreach (ListenerSettings listenerSetting in proxySettings.AdditionalListeners) {
					items.Add(listenerSetting);
				}
			}

			// Credential Tab
			if (commandSettings.Credentials != null) {
				items = this.credentialListView.Items;
				foreach (CredentialSettings credentialSettings in commandSettings.Credentials) {
					items.Add(credentialSettings);
				}
			}

			// Misc Tab
			// this.logLevelComboBox.SelectedItem is bound to this.LogLevel

			// update UI state
			if (runningProxy) {
				// modify the title
				this.Title = Properties.Resources.SettingsWindow_Title_ReadOnly;

				// disable input controls
				// Note that buttons are disabled through GetUIState().
				Control[] inputControls = new Control[] {
					this.autoDetectProxyCheckBox,
					this.hostNameTextBox,
					this.portTextBox,
					this.enableSystemSettingSwitchCheckBox,
					this.bypassLocalCheckBox,
					this.exclusionTextBox,
					this.retryTextBox,
					this.logLevelComboBox
				};
				Array.ForEach(inputControls, c => { c.IsEnabled = false; });
			}
			OnUIStateChanged(GetUIState());

			return;
		}

		#endregion


		#region overrides

		protected override void OnClosing(CancelEventArgs e) {
			// process the base class level task
			base.OnClosing(e);

			if (this.DialogResult ?? false) {
				// error check
				Control errorControl = GetErrorControl();
				if (errorControl != null) {
					ShowErrorDialog(Properties.Resources.SettingsWindow_Message_ErrorExists);
					errorControl.Focus();
					e.Cancel = true;
					return;
				}

				CommandSettings commandSettings = this.CommandSettings;

				// Listeners
				ProxySettings proxySettings = commandSettings.Proxy;
				ListenerSettings[] listeners = this.listenerListView.Items.Cast<ListenerSettings>().ToArray();
				Debug.Assert(0 < listeners.Length);
				proxySettings.MainListener = listeners[0];
				if (listeners.Length <= 1) {
					proxySettings.AdditionalListeners = null;
				} else {
					proxySettings.AdditionalListeners = new ArraySegment<ListenerSettings>(listeners, 1, listeners.Length - 1).ToArray();
				}

				// Credentials
				CredentialSettings[] credentials = this.credentialListView.Items.Cast<CredentialSettings>().ToArray();
				if (credentials.Length <= 0) {
					commandSettings.Credentials = null;
				} else {
					commandSettings.Credentials = credentials;
				}

				// other setting values are updated in real time 
			}

			return;
		}

		#endregion


		#region privates

		private UIStateFlags GetUIState() {
			// state checks
			if (this.runningProxy) {
				// only Cancel button is enabled
				return UIStateFlags.None;
			}

			// base state
			UIStateFlags state = UIStateFlags.Invariable;

			// reflect error state
			if (GetErrorControl() == null) {
				state |= UIStateFlags.OKEnabled;
				if (this.enableSaveAsDefault) {
					state |= UIStateFlags.SaveAsDefaultEnabled;
				}
			}

			// reflect listenerListView state
			int index = this.listenerListView.SelectedIndex;
			if (index != -1) {
				state |= UIStateFlags.EditListenerEnabled;
				if (index == 0) {
					state |= UIStateFlags.ResetListenerEnabled;
				} else {
					// Note that the default listener, that is the listener of index 0, cannot be removed
					Debug.Assert(0 < index);
					state |= (UIStateFlags.RemoveListenerEnabled | UIStateFlags.UpListenerEnabled);
				}
				if (index < this.listenerListView.Items.Count - 1) {
					state |= UIStateFlags.DownListenerEnabled;
				}
			}

			// reflect credentialListView state
			index = this.credentialListView.SelectedIndex;
			if (index != -1) {
				state |= (UIStateFlags.RemoveCredentialEnabled | UIStateFlags.EditCredentialEnabled);
			}

			return state;
		}

		private void UpdateUIState() {
			UIStateFlags newState = GetUIState();
			if (newState != this.uiState) {
				this.uiState = newState;
				OnUIStateChanged(newState);
			}

			return;
		}

		private void OnUIStateChanged(UIStateFlags newState) {
			// update state of UI elements
			UpdateIsEnabled(this.okButton, UIStateFlags.OKEnabled, newState);
			UpdateIsEnabled(this.saveAsDefaultButton, UIStateFlags.SaveAsDefaultEnabled, newState);
			UpdateIsEnabled(this.addListenerButton, UIStateFlags.AddListenerEnabled, newState);
			UpdateIsEnabled(this.removeListenerButton, UIStateFlags.RemoveListenerEnabled, newState);
			UpdateIsEnabled(this.editListenerButton, UIStateFlags.EditListenerEnabled, newState);
			UpdateIsEnabled(this.upListenerButton, UIStateFlags.UpListenerEnabled, newState);
			UpdateIsEnabled(this.downListenerButton, UIStateFlags.DownListenerEnabled, newState);
			UpdateIsEnabled(this.resetListenerButton, UIStateFlags.ResetListenerEnabled, newState);
			UpdateIsEnabled(this.addCredentialButton, UIStateFlags.AddCredentialEnabled, newState);
			UpdateIsEnabled(this.removeCredentialButton, UIStateFlags.RemoveCredentialEnabled, newState);
			UpdateIsEnabled(this.editCredentialButton, UIStateFlags.EditCredentialEnabled, newState);

			return;
		}

		private static void UpdateIsEnabled(Control control, UIStateFlags enabledFlag, UIStateFlags flags) {
			control.IsEnabled = ((flags & enabledFlag) != 0);
		}

		private void ShowErrorDialog(string message) {
			MessageBox.Show(this, message, this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
		}

		private Control GetErrorControl() {
			return this.validatableControls.Where(c => Validation.GetHasError(c)).FirstOrDefault();
		}

		private void UpdateSource(TextBox textBox) {
			// update the source
			textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
		}

		private void ClearError(TextBox textBox) {
			// clear errors on property binding
			BindingExpression binding = textBox.GetBindingExpression(TextBox.TextProperty);
			if (binding != null) {
				Validation.ClearInvalid(binding);
			}

			return;
		}

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

		private void AddListItem(ListView listView, object newItem) {
			// argument checks
			Debug.Assert(listView != null);
			Debug.Assert(newItem != null);

			// add the item
			listView.Items.Add(newItem);

			// update selection
			listView.SelectedItem = newItem;

			return;
		}

		private void ReplaceSelectedListItemWith(ListView listView, object newItem) {
			// argument checks
			Debug.Assert(listView != null);
			Debug.Assert(newItem != null);

			// replace the selected item
			int index = listView.SelectedIndex;
			if (index != -1) {
				// replace the item
				listView.Items[index] = newItem;

				// update selection
				listView.SelectedIndex = index;
			}

			return;
		}

		private void RemoveSelectedListItem(ListView listView) {
			// argument checks
			Debug.Assert(listView != null);

			// remove the selected item
			int index = listView.SelectedIndex;
			if (0 <= index) {
				// remove item
				ItemCollection listItems = listView.Items;
				listItems.RemoveAt(index);

				// update selection
				if (listItems.Count <= index) {
					index = listItems.Count - 1;
				}
				listView.SelectedIndex = index;
			}

			return;
		}

		private void MoveSelectedListItem(ListView listView, int step) {
			// argument checks
			Debug.Assert(listView != null);

			// move the selected ListenerSettings up
			int index = listView.SelectedIndex;
			if (0 < index) {
				ItemCollection items = listView.Items;

				// calculate new index
				int newIndex = index + step;
				if (newIndex < 0 || items.Count <= newIndex) {
					throw new ArgumentOutOfRangeException(nameof(step));
				}

				// move item
				object item = items[index];
				items.RemoveAt(index);
				items.Insert(newIndex, item);

				// update selection
				listView.SelectedIndex = newIndex;
			}

			return;
		}

		#endregion


		#region event handlers

		private void okButton_Click(object sender, RoutedEventArgs e) {
			this.DialogResult = true;
			this.SaveAsDefault = false;
		}

		private void saveAsDefaultButton_Click(object sender, RoutedEventArgs e) {
			this.DialogResult = true;
			this.SaveAsDefault = true;
		}

		private void autoDetectProxyCheckBox_Checked(object sender, RoutedEventArgs e) {
			// clear ActualProxy object
			this.CommandSettings.SystemSettingsSwitcher.ActualProxy = null;
			this.hostNameTextBox.IsEnabled = false;
			this.portTextBox.IsEnabled = false;
			ClearError(this.hostNameTextBox);
			ClearError(this.portTextBox);

			return;
		}

		private void autoDetectProxyCheckBox_Unchecked(object sender, RoutedEventArgs e) {
			// set ActualProxy object
			this.CommandSettings.SystemSettingsSwitcher.ActualProxy = new ActualProxySettings();
			this.hostNameTextBox.IsEnabled = true;
			this.portTextBox.IsEnabled = true;
			UpdateSource(this.hostNameTextBox);
			UpdateSource(this.portTextBox);

			return;
		}

		private void bypassLocalCheckBox_Checked(object sender, RoutedEventArgs e) {
			if (this.suppressUpdatingBypassLocalSource == false) {
				this.CommandSettings.SystemSettingsSwitcher.BypassLocal = true;
			}
		}

		private void bypassLocalCheckBox_Unchecked(object sender, RoutedEventArgs e) {
			if (this.suppressUpdatingBypassLocalSource == false) {
				this.CommandSettings.SystemSettingsSwitcher.BypassLocal = false;
			}
		}

		private void listenerListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			UpdateUIState();
		}

		private void addListenerButton_Click(object sender, RoutedEventArgs e) {
			try {
				ListView listView = this.listenerListView;
				ListenerSettings[] items = listView.Items.Cast<ListenerSettings>().ToArray();

				// create a new ListenerSettings
				ListenerSettings newItem = ListenerSettings.CreateDefaultListenerSettings(items);

				// prepare a validator
				Func<ListenerSettings, string> validator = (s) => {
					// check whether there is no listener which has the same end point
					ListenerSettings conflicting = items.Where(item => s.HasSameEndPointTo(item)).FirstOrDefault();
					return (conflicting == null) ? null : string.Format(Properties.Resources.SettingsWindow_Message_ConflictingListener, s.GetEndPoint());
				};

				// edit the ListenerSettings
				ListenerDialog dialog = new ListenerDialog(newItem, validator);
				dialog.Owner = this;
				if (dialog.ShowDialog() ?? false) {
					// add the ListenerSettings
					AddListItem(listView, dialog.ListenerSettings);
				}
			} catch (Exception exception) {
				ShowErrorDialog(exception.Message);
			}

			return;
		}

		private void removeListenerButton_Click(object sender, RoutedEventArgs e) {
			try {
				RemoveSelectedListItem(this.listenerListView);
			} catch (Exception exception) {
				ShowErrorDialog(exception.Message);
			}

			return;
		}

		private void editListenerButton_Click(object sender, RoutedEventArgs e) {
			try {
				ListView listView = this.listenerListView;

				// edit the selected ListenerSettings
				ListenerSettings originalItem = listView.SelectedItem as ListenerSettings;
				if (originalItem == null) {
					// this item is not editable
					return;
				}

				// create a clone of the selected ListenerSettings
				ListenerSettings newItem = ListenerSettings.Clone(originalItem);

				// prepare a validator
				ListenerSettings[] items = listView.Items.Cast<ListenerSettings>().ToArray();
				Func<ListenerSettings, string> validator = (s) => {
					// check there is no credential which has the same end point
					ListenerSettings conflicting = items.Where(item => (item != originalItem && s.HasSameEndPointTo(item))).FirstOrDefault();
					return (conflicting == null)? null: string.Format(Properties.Resources.SettingsWindow_Message_ConflictingListener, s.GetEndPoint());
				};

				// edit the clone
				ListenerDialog dialog = new ListenerDialog(newItem, validator);
				dialog.Owner = this;
				if (dialog.ShowDialog() ?? false) {
					ReplaceSelectedListItemWith(listView, dialog.ListenerSettings);
				}
			} catch (Exception exception) {
				ShowErrorDialog(exception.Message);
			}

			return;
		}

		private void upListenerButton_Click(object sender, RoutedEventArgs e) {
			try {
				MoveSelectedListItem(this.listenerListView, -1);
			} catch (Exception exception) {
				ShowErrorDialog(exception.Message);
			}

			return;
		}

		private void downListenerButton_Click(object sender, RoutedEventArgs e) {
			try {
				MoveSelectedListItem(this.listenerListView, 1);
			} catch (Exception exception) {
				ShowErrorDialog(exception.Message);
			}

			return;
		}

		private void resetListenerButton_Click(object sender, RoutedEventArgs e) {
			try {
				// reset the main ListenerSettings
				ListenerSettings defaultItem = new ListenerSettings();
				ItemCollection items = this.listenerListView.Items;
				items[0] = defaultItem;

				// remove ListenerSettings which have the default end point
				int index = 1;  // do not contain the main ListenerSettings
				while (index < items.Count) {
					ListenerSettings listenerSettings = (ListenerSettings)items[index];
					if (defaultItem.HasSameEndPointTo(listenerSettings)) {
						items.RemoveAt(index);
						// Do not increment index. The next item comes here.
					} else {
						++index;
					}
				}
			} catch (Exception exception) {
				ShowErrorDialog(exception.Message);
			}

			return;
		}

		private void credentialListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			UpdateUIState();
		}

		private void addCredentialButton_Click(object sender, RoutedEventArgs e) {
			try {
				ListView listView = this.credentialListView;

				// create a new CredentialSettings
				CredentialSettings newItem = new CredentialSettings();
				// set its default persistence to Process (i.e. not persistent)
				newItem.Persistence = CredentialPersistence.Process;

				// prepare a validator
				CredentialSettings[] items = listView.Items.Cast<CredentialSettings>().ToArray();
				Func<CredentialSettings, string> validator = (s) => {
					// check whether there is no credential which is for the same end point
					CredentialSettings conflicting = items.Where(item => s.HasSameEndPoint(item)).FirstOrDefault();
					return (conflicting == null) ? null : string.Format(Properties.Resources.SettingsWindow_Message_ConflictingCredential, s.EndPoint);
				};

				// edit the CredentialSettings
				CredentialDialog dialog = new CredentialDialog(newItem, validator, endPointEditable: true);
				dialog.Owner = this;
				if (dialog.ShowDialog() ?? false) {
					// add the CredentialSettings
					AddListItem(listView, dialog.CredentialSettings);
				}
			} catch (Exception exception) {
				ShowErrorDialog(exception.Message);
			}

			return;
		}

		private void removeCredentialButton_Click(object sender, RoutedEventArgs e) {
			try {
				RemoveSelectedListItem(this.credentialListView);
			} catch (Exception exception) {
				ShowErrorDialog(exception.Message);
			}

			return;
		}

		private void editCredentialButton_Click(object sender, RoutedEventArgs e) {
			try {
				ListView listView = this.credentialListView;

				// get the selected CredentialSettings
				CredentialSettings originalItem = listView.SelectedItem as CredentialSettings;
				if (originalItem == null) {
					// this item is not editable
					return;
				}

				// create a clone of the selected CredentialSettings
				CredentialSettings newItem = CredentialSettings.Clone(originalItem);

				// prepare a validator
				CredentialSettings[] items = listView.Items.Cast<CredentialSettings>().ToArray();
				Func<CredentialSettings, string> validator = (s) => {
					// check there is no credential which is for the same end point
					CredentialSettings conflicting = items.Where(item => (item != originalItem && s.HasSameEndPoint(item))).FirstOrDefault();
					return (conflicting == null) ? null : string.Format(Properties.Resources.SettingsWindow_Message_ConflictingCredential, s.EndPoint);
				};

				// edit the clone
				CredentialDialog dialog = new CredentialDialog(newItem, validator, endPointEditable: true);
				dialog.Owner = this;
				if (dialog.ShowDialog() ?? false) {
					ReplaceSelectedListItemWith(listView, dialog.CredentialSettings);
				}
			} catch (Exception exception) {
				ShowErrorDialog(exception.Message);
			}

			return;
		}

		#endregion
	}
}
