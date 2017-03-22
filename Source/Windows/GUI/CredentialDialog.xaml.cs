using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MAPE.Command;
using MAPE.Command.Settings;


namespace MAPE.Windows.GUI {
	public partial class CredentialDialog: Window {
		#region data

		public CredentialSettings CredentialSettings {
			get; private set;
		}

		public readonly Func<CredentialSettings, string> validator;

		#endregion


		#region creation and disposal

		public CredentialDialog(CredentialSettings credentialSettings, Func<CredentialSettings, string> validator = null, bool endPointEditable = false) {
			// argument checks
			if (credentialSettings == null) {
				throw new ArgumentNullException(nameof(credentialSettings));
			}
			// validator can be null
			if (endPointEditable && credentialSettings.Persistence == CredentialPersistence.Session) {
				// session mode is useless when endPoint is editable
				throw new ArgumentException($"Its 'Persistence' property cannot be 'Session' if '{nameof(endPointEditable)}' is true.", nameof(credentialSettings));
			}

			// initialize members
			this.CredentialSettings = credentialSettings;
			this.validator = validator;

			// initialize components
			InitializeComponent();
			this.DataContext = credentialSettings;

			// EndPoint
			if (endPointEditable) {
				// show TextBox to edit EndPoint
				this.endPointLabel.Visibility = Visibility.Visible;
				this.endPointTextBox.Visibility = Visibility.Visible;
				this.endPointTextBox.IsEnabled = true;
				this.descriptionTextBlock.Visibility = Visibility.Hidden;
				this.sessionRadioButton.IsEnabled = false;	// useless in this mode 

				// this.endPointTextBox.Text is bound to credentialSettings.EndPoint
			} else {
				// show EndPoint which asks your credential 
				string endPoint = credentialSettings.EndPoint;
				if (endPoint == null) {
					endPoint = "(unidentified proxy)";
				} else if (endPoint.Length == 0) {
					endPoint = "(all proxies)";
				}
				this.descriptionTextBlock.Text = string.Format(Properties.Resources.CredentialDialog_descriptionTextBlock_Text, endPoint);

				this.endPointLabel.Visibility = Visibility.Hidden;
				this.endPointTextBox.Visibility = Visibility.Hidden;
				this.endPointTextBox.IsEnabled = false;
				this.descriptionTextBlock.Visibility = Visibility.Visible;
			}

			// UserName
			// this.userNameTextBox.Text is bound to credentialSettings.UserName

			// Password
			this.passwordBox.Password = credentialSettings.Password;

			// Persistence
			// update persistence radio buttons
			RadioButton radioButton = null;
			switch (credentialSettings.Persistence) {
				case CredentialPersistence.Session:
					radioButton = this.sessionRadioButton;
					break;
				case CredentialPersistence.Persistent:
					radioButton = this.persistentRadioButton;
					break;
				default:
					// CredentialPersistence.Process is default
					radioButton = this.processRadioButton;
					break;
			}
			radioButton.IsChecked = true;

			// EnableAssumptionMode
			// this.enableAssumptionModeCheckBox.IsChecked is bound to credentialSettings.EnableAssumptionMode

			// set initial focus
			Control control;
			if (endPointEditable) {
				control = this.endPointTextBox;
			} else {
				if (string.IsNullOrEmpty(credentialSettings.UserName)) {
					control = this.userNameTextBox;
				} else {
					control = this.passwordBox;
				}
			}
			control.Focus();

			return;
		}

		#endregion


		#region overrides

		protected override void OnClosing(CancelEventArgs e) {
			// close this class level
			if (this.DialogResult ?? false) {
				// prepare output CredentialSettings
				this.CredentialSettings.Password = this.passwordBox.Password;
				// EndPoint, UserName, Persistence and EnableAssumptionMode are updated in real time 
	
				// validate
				if (this.validator != null) {
					string errorMessage = validator(this.CredentialSettings);
					if (errorMessage != null) {
						// it is not acceptable. Cancel closing.
						MessageBox.Show(this, errorMessage, this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
						e.Cancel = true;
						if (this.endPointLabel.Visibility == Visibility.Visible) {
							this.endPointLabel.Focus();
						}
						return;
					}
				}				
			}

			// close the base class level
			base.OnClosing(e);
		}

		#endregion


		#region event handlers

		private void okButton_Click(object sender, RoutedEventArgs e) {
			this.DialogResult = true;
		}

		private void sessionRadioButton_Checked(object sender, RoutedEventArgs e) {
			this.CredentialSettings.Persistence = CredentialPersistence.Session;
		}

		private void processRadioButton_Checked(object sender, RoutedEventArgs e) {
			this.CredentialSettings.Persistence = CredentialPersistence.Process;
		}

		private void persistentRadioButton_Checked(object sender, RoutedEventArgs e) {
			this.CredentialSettings.Persistence = CredentialPersistence.Persistent;
		}

		#endregion
	}
}
