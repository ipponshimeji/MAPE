using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MAPE.Command;


namespace MAPE.Windows.GUI {
	public partial class CredentialDialog: Window {
		#region data

		public string EndPoint { get; set; } = null;

		public CredentialInfo Credential { get; set; } = null;

		#endregion


		#region creation and disposal

		public CredentialDialog() {
			InitializeComponent();
		}

		#endregion


		#region overrides

		protected override void OnInitialized(EventArgs e) {
			// initialize the base class level
			base.OnInitialized(e);

			// initialize this class level
			this.Icon = App.Current.OnIcon;

			string endPoint = this.EndPoint ?? "(unidentified proxy)";
			this.descriptionTextBlock.Text = string.Format(Properties.Resources.CredentialDialog_Description, endPoint);

			CredentialInfo credential = this.Credential;
			if (credential == null) {
				this.userNameTextBox.Text = string.Empty;
				this.sessionRadioButton.IsChecked = true;
			} else {
				this.userNameTextBox.Text = credential.UserName ?? string.Empty;
				RadioButton radioButton = null;
				switch (credential.Persistence) {
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
			}
			this.passwordBox.Password = string.Empty;   // Do not give default value

			// set initial focus on userNameTextBox
			this.userNameTextBox.Focus();

			return;
		}

		#endregion


		#region event handlers

		private void okButton_Click(object sender, RoutedEventArgs e) {
			// check result
			string endPoint = this.EndPoint ?? string.Empty;
			string userName = this.userNameTextBox.Text;
			string password = this.passwordBox.Password;
			CredentialPersistence persistence;
			if (this.sessionRadioButton.IsChecked ?? false) {
				persistence = CredentialPersistence.Session;
			} else if (this.persistentRadioButton.IsChecked ?? false) {
				persistence = CredentialPersistence.Persistent;
			} else {
				// CredentialPersistence.Process is default
				persistence = CredentialPersistence.Process;
			}
			bool enableAssumptionMode = this.enableAssumptionModeCheckBox.IsChecked ?? false;

			// commit the result
			this.Credential = new CredentialInfo(endPoint, userName, password, persistence, enableAssumptionMode);
			this.DialogResult = true;

			return;
		}

		#endregion
	}
}
