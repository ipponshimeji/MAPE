using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;


namespace MAPE.Windows.GUI {
	public partial class VersionInfoWindow: Window {
		#region creation and disposal

		public VersionInfoWindow() {
			InitializeComponent();
		}

		#endregion


		#region event handlers

		private void okButton_Click(object sender, RoutedEventArgs e) {
			this.Close();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e) {
			// initialize UI
			Assembly assembly = typeof(VersionInfoWindow).Assembly;
			string version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
			string copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
			string configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
			
			if (string.IsNullOrEmpty(version)) {
				version = "(unknown)";
			}
			if (string.IsNullOrEmpty(configuration) == false) {
				version = $"{version} ({configuration})";
			}

			this.versionLabel.Content = "version " + version;
			this.copyrightLabel.Content = copyright ?? string.Empty;

			return;
		}

		#endregion
	}
}
