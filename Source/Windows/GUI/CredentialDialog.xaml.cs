using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MAPE.Command;
using MAPE.Command.Settings;


namespace MAPE.Windows.GUI {
	public partial class CredentialDialog: Window {
		#region data

		private string endPoint = null;

		private CredentialSettings credential = null;

		#endregion


		#region properties

		public string EndPoint {
			get {
				return this.endPoint;
			}
			set {
				if (this.endPoint != value) {
					this.endPoint = value;

					// update descriptionTextBlock
					this.descriptionTextBlock.Text = string.Format(Properties.Resources.CredentialDialog_Description, value ?? "(unidentified proxy)");
				}
			}
		}

		public CredentialSettings Credential {
			get {
				return this.credential;
			}
			set {
				this.credential = value;
				if (value == null) {
					this.userNameTextBox.Text = string.Empty;
					this.sessionRadioButton.IsChecked = true;
					this.enableAssumptionModeCheckBox.IsChecked = true;
				} else {
					// update userNameTextBox
					this.userNameTextBox.Text = value.UserName ?? string.Empty;

					// update persistence radio buttons
					RadioButton radioButton = null;
					switch (value.Persistence) {
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

					// update enableAssumptionModeCheckBox
					this.enableAssumptionModeCheckBox.IsChecked = value.EnableAssumptionMode;
				}
			}
		}

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

			// Do not give default value for password
			this.passwordBox.Password = string.Empty;

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
			this.Credential = new CredentialSettings(endPoint, userName, password, persistence, enableAssumptionMode);
			this.DialogResult = true;

			return;
		}

		#endregion
	}
}
