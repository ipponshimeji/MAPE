using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MAPE.Server.Settings;


namespace MAPE.Windows.GUI {
	/// <summary>
	/// ListenerDialog.xaml の相互作用ロジック
	/// </summary>
	public partial class ListenerDialog: Window {
		#region data

		public ListenerSettings ListenerSettings {
			get; private set;
		}

		public readonly Func<ListenerSettings, string> validator;

		#endregion


		#region properties - data binding adapters

		public string Address {
			get {
				return this.ListenerSettings.Address.ToString();
			}
			set {
				this.ListenerSettings.Address = IPAddress.Parse(value);
			}
		}

		public int Port {
			get {
				return this.ListenerSettings.Port;
			}
			set {
				this.ListenerSettings.Port = value;
			}
		}

		public int Backlog {
			get {
				return this.ListenerSettings.Backlog;
			}
			set {
				this.ListenerSettings.Backlog = value;
			}
		}

		#endregion


		#region creation and disposal

		public ListenerDialog(ListenerSettings listenerSettings, Func<ListenerSettings, string> validator = null) {
			// argument checks
			if (listenerSettings == null) {
				throw new ArgumentNullException(nameof(listenerSettings));
			}
			// validator can be null

			// initialize members
			this.ListenerSettings = listenerSettings;
			this.validator = validator;

			// initialize components
			InitializeComponent();
			this.DataContext = this;

			// Address
			// this.addressTextBox.Text is bound to this.Address

			// Port
			// this.portTextBox.Text is bound to this.Port

			// Backlog
			// this.backlogTextBox.Text is bound to this.Backlog

			return;
		}

		#endregion


		#region overrides

		protected override void OnClosing(CancelEventArgs e) {
			// close this class level
			if (this.DialogResult ?? false) {
				// validate
				if (this.validator != null) {
					string errorMessage = validator(this.ListenerSettings);
					if (errorMessage != null) {
						// It is not acceptable. Cancel closing.
						MessageBox.Show(this, errorMessage, this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
						e.Cancel = true;
						this.portTextBox.Focus();
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

		#endregion
	}
}
