using System;
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
			this.actualProxy.SystemSettingsSwitcherSettings = settings.SystemSettingsSwitcher;
			this.actualProxy.DefaultActualProxyHostName = setupContext.DefaultActualProxyHostName;
			this.actualProxy.DefaultActualProxyPort = setupContext.DefaultActualProxyPort;
			if (setupContext.NeedActualProxy) {
				StringBuilder buf = new StringBuilder(Windows.Properties.Resources.Setup_AuthenticationProxy_Description_NeedToChange);
				if (setupContext.IsDefaultActualProxyProvided) {
					buf.AppendLine();
					buf.Append(Windows.Properties.Resources.Setup_Description_DefaultValueProvided);
				}
				this.authenticationProxyDescriptionTextBlock.Text = buf.ToString();
				this.actualProxy.AutoDetectEnabled = false;
			} else {
				this.authenticationProxyDescriptionTextBlock.Text = Windows.Properties.Resources.Setup_Description_NoNeedToChange;
				this.actualProxy.AutoDetectEnabled = setupContext.ProxyDetected;
			}

			// System Settings Switc tab
			this.systemSettingsSwitcher.SystemSettingsSwitcherSettings = settings.SystemSettingsSwitcher;
			if (setupContext.NeedProxyOverride) {
				StringBuilder buf = new StringBuilder(Windows.Properties.Resources.Setup_SystemSettingsSwitch_Description_NeedToChange);
				if (string.IsNullOrEmpty(setupContext.DefaultProxyOverride) == false) {
					buf.AppendLine();
					buf.Append(Windows.Properties.Resources.Setup_Description_DefaultValueProvided);
				}
				this.systemSettingsSwitcherDescriptionTextBlock.Text = buf.ToString();
			} else {
				this.systemSettingsSwitcherDescriptionTextBlock.Text = Windows.Properties.Resources.Setup_Description_NoNeedToChange;
			}

			// Test tab
			this.testDescriptionTextBlock.Text = Windows.Properties.Resources.Setup_Test_Description;
			this.targetUrlTextBox.Text = "https://www.google.com/";

			// Finish tab
			this.finishingDescriptionTextBlock.Text = Windows.Properties.Resources.Setup_Finishing_Description;

			OnUIStateChanged(DetectUIState());

			return;
		}

		#endregion


		#region privates

		private void ShowErrorDialog(string message) {
			MessageBox.Show(this, message, this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
		}

		private UIStateFlags DetectUIState() {
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

		private void UpdateUIState() {
			UIStateFlags newState = DetectUIState();
			if (newState != this.uiState) {
				this.uiState = newState;
				OnUIStateChanged(newState);
			}

			return;
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

		private bool CanMoveNext() {
			// check error in each page
			for (int i = 0; i <= this.doneIndex; ++i) {
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
			if (CanMoveNext()) {
				this.DialogResult = true;
			}
		}

		private void nextButton_Click(object sender, RoutedEventArgs e) {
			try {
				// check state
				if (CanMoveNext() == false) {
					return;
				}

				int currentIndex = this.setupTab.SelectedIndex;
				Debug.Assert(currentIndex < this.setupTab.Items.Count - 1);
				if (currentIndex == this.doneIndex) {
					// enable the next tab item
					++this.doneIndex;
					UpdateUIState();
				}

				// move to the next tab item
				this.setupTab.SelectedIndex = ++currentIndex;
				UpdateUIState();
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

		private void testButton_Click(object sender, RoutedEventArgs e) {
			try {
				this.testResultTextBlock.Text = string.Empty;
				this.Command.Test(this.SetupContext.Settings, this.targetUrlTextBox.Text);
				this.testResultTextBlock.Foreground = Brushes.Green;
				this.testResultTextBlock.Text = "OK";
				this.tested = true;
			} catch (Exception exception) {
				this.testResultTextBlock.Foreground = Brushes.Red;
				this.testResultTextBlock.Text = exception.Message;
				this.tested = false;
			}

			return;
		}

		#endregion
	}
}
