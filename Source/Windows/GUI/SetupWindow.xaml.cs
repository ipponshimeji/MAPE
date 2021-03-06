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
using MAPE.Command;
using MAPE.Windows.Settings;
using MAPE.Windows.GUI.Settings;


namespace MAPE.Windows.GUI {
	public partial class SetupWindow: Window {
		#region types

		[Flags]
		public enum UIStateFlags {
			CancelEnabled = 0x01,
			FinishEnabled = 0x02,
			NextEnabled = 0x04,
			BackEnabled = 0x08,
			AuthenticationProxyTabEnabled = 0x10,
			SystemSettingsSwitchTabEnabled = 0x20,
			TestTabEnabled = 0x40,
			FinishingTabEnabled = 0x80,

			None = 0,
			Invariable = CancelEnabled | AuthenticationProxyTabEnabled,
			InitialState = Invariable,
		}

		#endregion


		#region data

		internal readonly Command Command;

		public SetupContextForWindows SetupContext {
			get; private set;
		}

		private UIStateFlags uiState = UIStateFlags.InitialState;

		private int doneIndex = 0;

		private bool tested = false;

		private bool testing = false;

		#endregion


		#region creation and disposal

		internal SetupWindow(Command command, SetupContextForWindows setupContext) {
			// argument checks
			if (command == null) {
				throw new ArgumentNullException(nameof(command));
			}
			if (setupContext == null) {
				throw new ArgumentNullException(nameof(setupContext));
			}
			CommandForWindowsSettings settings = setupContext.Settings;

			// initialize members
			this.Command = command;
			this.SetupContext = setupContext;

			// initialize components
			InitializeComponent();

			// Authentication Proxy tab
			SystemSettingsSwitcherForWindowsSettings systemSettingsSwitcherSettings = settings.SystemSettingsSwitcher;
			if (setupContext.NeedActualProxy) {
				StringBuilder buf = new StringBuilder(Windows.Properties.Resources.Setup_AuthenticationProxy_Description_NeedToChange);
				if (setupContext.IsDefaultActualProxyProvided) {
					buf.AppendLine();
					buf.AppendLine();
					buf.Append(Windows.Properties.Resources.Setup_Description_DefaultValueProvided);
				}
				this.authenticationProxyDescriptionTextBlock.Text = buf.ToString();
				if (systemSettingsSwitcherSettings.ActualProxy == null) {
					systemSettingsSwitcherSettings.ActualProxy = setupContext.CreateActualProxySettings();
				}
			} else {
				this.authenticationProxyDescriptionTextBlock.Text = Windows.Properties.Resources.Setup_Description_NoNeedToChange;
				if (setupContext.ProxyDetected == false && systemSettingsSwitcherSettings.ActualProxy == null) {
					systemSettingsSwitcherSettings.ActualProxy = setupContext.CreateActualProxySettings();
				}
			}
			this.actualProxy.SystemSettingsSwitcherSettings = systemSettingsSwitcherSettings;

			// System Settings Switch tab
			this.systemSettingsSwitcher.SystemSettingsSwitcherSettings = settings.SystemSettingsSwitcher;
			if (setupContext.NeedProxyOverride) {
				this.systemSettingsSwitcherDescriptionTextBlock.Text = Windows.Properties.Resources.Setup_Description_Confirm;
			} else {
				this.systemSettingsSwitcherDescriptionTextBlock.Text = Windows.Properties.Resources.Setup_Description_NoNeedToChange;
			}

			// Test tab
			this.testDescriptionTextBlock.Text = Windows.Properties.Resources.Setup_Test_Description;
			this.targetUrlTextBox.Text = SystemSettingsSwitcher.GetTestUrl();

			// Finish tab
			string description = string.Concat(Windows.Properties.Resources.Setup_Finishing_Description, Environment.NewLine, Environment.NewLine, Properties.Resources.SetupWindow_finishingDescriptionTextBox_Text_Addition);
			this.finishingDescriptionTextBlock.Text = description;

			UpdateUIState();

			return;
		}

		#endregion


		#region privates

		private void ShowErrorDialog(string message) {
			MessageBox.Show(this, message, this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
		}

		private void SetUIState(UIStateFlags newState) {
			if (newState != this.uiState) {
				this.uiState = newState;
				OnUIStateChanged(newState);
			}

			return;
		}

		private void UpdateUIState() {
			SetUIState(DetectUIState());
			return;
		}

		private UIStateFlags DetectUIState() {
			// state checks
			if (this.testing) {
				return UIStateFlags.None;
			}

			// base state
			UIStateFlags state = UIStateFlags.Invariable;

			int currentIndex = this.setupTab.SelectedIndex;
			if (0 < currentIndex) {
				state |= UIStateFlags.BackEnabled;				
			}

			int doneIndex = this.doneIndex;
			if (1 <= doneIndex) {
				state |= UIStateFlags.SystemSettingsSwitchTabEnabled;
			}
			if (2 <= doneIndex) {
				state |= UIStateFlags.TestTabEnabled;
			}
			if (3 <= doneIndex) {
				state |= UIStateFlags.FinishingTabEnabled;
				state |= UIStateFlags.FinishEnabled;
			}

			if (currentIndex < this.setupTab.Items.Count - 1) {
				state |= UIStateFlags.NextEnabled;
			}

			return state;
		}

		private void OnUIStateChanged(UIStateFlags newState) {
			// update state of UI elements
			UpdateIsEnabled(this.cancelButton, UIStateFlags.CancelEnabled, newState);
			UpdateIsEnabled(this.finishButton, UIStateFlags.FinishEnabled, newState);
			UpdateIsEnabled(this.nextButton, UIStateFlags.NextEnabled, newState);
			UpdateIsEnabled(this.backButton, UIStateFlags.BackEnabled, newState);
			UpdateIsEnabled(this.authenticationProxyTabItem, UIStateFlags.AuthenticationProxyTabEnabled, newState);
			UpdateIsEnabled(this.systemSettingsSwitchTabItem, UIStateFlags.SystemSettingsSwitchTabEnabled, newState);
			UpdateIsEnabled(this.testTabItem, UIStateFlags.TestTabEnabled, newState);
			UpdateIsEnabled(this.finishingTabItem, UIStateFlags.FinishingTabEnabled, newState);

			return;
		}

		private static void UpdateIsEnabled(Control control, UIStateFlags enabledFlag, UIStateFlags flags) {
			control.IsEnabled = ((flags & enabledFlag) != 0);
		}

		private void MoveNextTabItem() {
			int currentIndex = this.setupTab.SelectedIndex;
			Debug.Assert(currentIndex < this.setupTab.Items.Count - 1);

			// check state
			if (CanMoveNext(currentIndex) == false) {
				return;
			}

			if (currentIndex == this.doneIndex) {
				// enable the next tab item
				++this.doneIndex;
				UpdateUIState();
			}

			// move to the next tab item
			this.setupTab.SelectedIndex = ++currentIndex;
			UpdateUIState();

			return;
		}

		private Control HasError(int currentIndex) {
			switch (currentIndex) {
				case 0:
					// Authentication Proxy tab
					return this.actualProxy.GetErrorControl(setupMode: true);
				case 2:
					if (this.tested == false) {
						return this.testButton;
					}
					break;
			}

			return null;
		}

		private bool CanMoveNext(int currentIndex) {
			// argument checks
			Debug.Assert(0 <= currentIndex && currentIndex <= this.doneIndex);

			// check error in each page
			for (int i = 0; i <= currentIndex; ++i) {
				Control errorControl = HasError(i);
				if (errorControl != null) {
					string message = Properties.Resources.SetupWindow_Message_Error;
					if (i == 2) {
						MessageBoxResult result = MessageBox.Show(this, Properties.Resources.SetupWindow_Message_NotTested, this.Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
						if (result == MessageBoxResult.Yes) {
							continue;
						}
					} else {
						ShowErrorDialog(Properties.Resources.SetupWindow_Message_Error);
					}
					this.setupTab.SelectedIndex = i;
					errorControl.Focus();
					return false;
				}
			}

			return true;
		}

		#endregion


		#region event handlers

		private void finishButton_Click(object sender, RoutedEventArgs e) {
			if (CanMoveNext(3)) {
				this.DialogResult = true;
			}
		}

		private void nextButton_Click(object sender, RoutedEventArgs e) {
			try {
				MoveNextTabItem();
			} catch (Exception exception) {
				ShowErrorDialog(exception.Message);
			}
		}

		private void backButton_Click(object sender, RoutedEventArgs e) {
			int currentIndex = this.setupTab.SelectedIndex;
			if (0 < currentIndex) {
				this.setupTab.SelectedIndex = --currentIndex;
				UpdateUIState();
			}

			return;
		}

		private async void testButton_Click(object sender, RoutedEventArgs e) {
			// state checks
			if (this.testing) {
				return;
			}

			this.testing = true;
			try {
				// update UI state
				UpdateUIState();

				// test connection
				this.testResultTextBlock.Text = string.Empty;
				string targetUrl = this.targetUrlTextBox.Text;
				await this.Command.TestAsync(this.SetupContext.Settings, targetUrl);

				// display the result
				this.testResultTextBlock.Foreground = Brushes.Green;
				this.testResultTextBlock.Text = "OK";
				this.tested = true;
			} catch (Exception exception) {
				// display the error
				this.testResultTextBlock.Foreground = Brushes.Red;
				this.testResultTextBlock.Text = exception.Message;
				this.tested = false;
			} finally {
				// restore UI state
				this.testing = false;
				UpdateUIState();
			}

			return;
		}

		private void Window_ContentRendered(object sender, EventArgs e) {
			if (this.SetupContext.ShouldResetProxyOverride && this.SetupContext.NeedActualProxy == false) {
				// resetting ProxyOverride value
				// no interest on Authentication Proxy tab
				try {
					MoveNextTabItem();
				} catch {
					// continue
				}
			}
		}

		private async void setupTab_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (this.setupTab.SelectedIndex == 1) {
				// System Settings Switch tab
				if (this.SetupContext.ShouldResetProxyOverride) {
					// the case new MAPE recommends to reset the ProxyOverride
					string defaultProxyOverride = this.SetupContext.DefaultProxyOverride;
					if (string.CompareOrdinal(this.systemSettingsSwitcher.ProxyOverride, defaultProxyOverride) != 0) {
						// wait a moment
						// Otherwise, rendering of the selected tab item would not complete
						// and the contents of the old tab item would remain during opening the message box.
						this.setupTab.IsEnabled = false;
						try {
							await Task.Delay(700);
						} finally {
							this.setupTab.IsEnabled = true;
						}

						// the case the current setting is not the recommended setting
						MessageBoxResult result = MessageBox.Show(
							this,
							Windows.Properties.Resources.Setup_SystemSettingsSwitch_Description_NeedToReset,
							Properties.Resources.SetupWindow_UpdateMessageBox_Title,
							MessageBoxButton.YesNo
						);
						if (result == MessageBoxResult.Yes) {
							this.systemSettingsSwitcher.ProxyOverride = defaultProxyOverride;
						}
					}
				}
			}
		}

		#endregion
	}
}
