using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;


namespace MAPE.Windows.GUI {
	public partial class AboutWindow: Window {
		#region creation and disposal

		public AboutWindow() {
			InitializeComponent();
		}

		#endregion


		#region overrides

		protected override void OnInitialized(EventArgs e) {
			// initialize the base class level
			base.OnInitialized(e);

			// initialize this class level

			// icons
			BitmapFrame onIcon = App.Current.OnIcon;
			this.Icon = onIcon;
			this.appImage.Source = onIcon;

			// version and copyright
			Assembly assembly = typeof(AboutWindow).Assembly;
			string version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
			string copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
			string configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;

			if (string.IsNullOrEmpty(version)) {
				version = "(unknown)";
			}
			if (string.IsNullOrEmpty(configuration) == false) {
				version = $"{version} ({configuration})";
			}

			this.versionLabel.Content = "Version " + version;
			this.copyrightLabel.Content = copyright ?? string.Empty;

			return;
		}

		#endregion


		#region event handlers

		private void okButton_Click(object sender, RoutedEventArgs e) {
			this.Close();
		}

		#endregion
	}
}
