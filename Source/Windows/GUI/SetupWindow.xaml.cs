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

		public CommandForWindowsGUISettings CommandSettings {
			get; private set;
		}

		private UIStateFlags uiState = UIStateFlags.InitialState;

		private int doneIndex = 0;

		#endregion


		#region creation and disposal

		public SetupWindow(CommandForWindowsGUISettings commandSettings) {
			// argument checks
			if (commandSettings == null) {
				throw new ArgumentNullException(nameof(commandSettings));
			}

			// initialize members
			this.CommandSettings = commandSettings;

			// initialize components
			InitializeComponent();

			OnUIStateChanged(GetUIState());

			return;
		}

		#endregion


		#region privates

		private UIStateFlags GetUIState() {
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

		#endregion


		#region event handlers

		private void finishButton_Click(object sender, RoutedEventArgs e) {
			this.DialogResult = true;
		}

		#endregion
	}
}
