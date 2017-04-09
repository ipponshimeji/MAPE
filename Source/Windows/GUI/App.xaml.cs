using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using MAPE.Utils;
using MAPE.Windows.GUI.Settings;
using AssemblyResources = MAPE.Windows.GUI.Properties.Resources;


namespace MAPE.Windows.GUI {
	public partial class App: Application {
		#region types

		[Flags]
		public enum UIStateFlags {
			ExitEnabled = 0x01,
			StartEnabled = 0x02,
			StopEnabled = 0x04,
			SettingsEnabled = 0x08,
			AboutEnabled = 0x10,

			None = 0,
			InitialState = None,
		}

		#endregion


		#region data

		internal readonly Command Command;

		internal UIStateFlags UIState { get; private set; }

		private NotifyIconComponent notifyIcon;

		private BitmapFrame onIcon;

		private BitmapFrame offIcon;

		private MainWindow mainWindow;

		#endregion


		#region properties

		internal static new App Current {
			get {
				return (App)Application.Current;
			}
		}

		internal BitmapFrame OnIcon {
			get {
				return this.onIcon;
			}
		}

		internal BitmapFrame OffIcon {
			get {
				return this.offIcon;
			}
		}

		internal bool IsProxyRunning {
			get {
				return this.Command.IsProxyRunning;
			}
		}

		#endregion


		#region events

		public event EventHandler UIStateChanged = null;

		#endregion


		#region creation and disposal

		internal App(Command command) : base() {
			// argument checks
			if (command == null) {
				throw new ArgumentNullException(nameof(command));
			}

			// initialize members
			this.Command = command;
			this.UIState = UIStateFlags.InitialState;
			this.onIcon = null;
			this.offIcon = null;
			this.notifyIcon = null;
			this.mainWindow = null;

			return;
		}

		#endregion


		#region methods

		internal void StartProxy() {
			// state checks
			Command command = this.Command;
			Debug.Assert(command != null);
			if ((this.UIState & UIStateFlags.StartEnabled) == 0) {
				// currently not enabled
				// maybe a queued event is dispatched belatedly 
				return;
			}

			// start the proxy
			command.StartProxy();

			return;
		}

		internal void StopProxy() {
			// state checks
			Command command = this.Command;
			if (command == null) {
				throw new InvalidOperationException();
			}
			if ((this.UIState & UIStateFlags.StopEnabled) == 0) {
				// currently not enabled
				// maybe a queued event is dispatched belatedly 
				return;
			}

			// stop the proxy
			// ToDo: should use async?
			command.StopProxy(5000);

			return;
		}

		internal MainWindow OpenMainWindow() {
			MainWindow window = this.mainWindow;
			if (window != null) {
				window.Activate();
			} else {
				window = new MainWindow(this, this.Command.GUISettings.MainWindow);
				window.UIStateChanged += mainWindow_UIStateChanged;
				window.Closed += mainWindow_Closed;
				this.mainWindow = window;
				window.Show();
			}

			return window;
		}

		internal CommandForWindowsGUISettings ShowSetupWindow(CommandForWindowsGUISettings settings) {
			CommandForWindowsGUISettings newSettings = null;
			try {
				newSettings = OpenMainWindow().ShowSetupWindow(settings);
			} catch (Exception exception) {
				ErrorMessage(exception.Message);
			}

			return newSettings;
		}

		internal void ErrorMessage(string message) {
			MessageBox.Show(message, this.Command.ComponentName, MessageBoxButton.OK, MessageBoxImage.Error);
		}

		#endregion


		#region overrides

		protected override void OnStartup(StartupEventArgs e) {
			// process the base class level tasks
			base.OnStartup(e);

			// process this class level tasks
			this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

			this.onIcon = BitmapFrame.Create(new Uri("pack://siteoforigin:,,,/Resources/OnIcon.ico"));
			this.offIcon = BitmapFrame.Create(new Uri("pack://siteoforigin:,,,/Resources/OffIcon.ico"));

			// setup task tray menu
			NotifyIconComponent notifyIcon = new NotifyIconComponent();
			notifyIcon.StartMenuItem.Click += this.StartMenuItem_Click;
			notifyIcon.StopMenuItem.Click += this.StopMenuItem_Click;
			notifyIcon.OpenMenuItem.Click += this.OpenMenuItem_Click;
			notifyIcon.SettingsMenuItem.Click += this.SettingsMenuItem_Click;
			notifyIcon.AboutMenuItem.Click += this.AboutMenuItem_Click;
			notifyIcon.ExitMenuItem.Click += this.ExitMenuItem_Click;
			this.notifyIcon = notifyIcon;

			// misc
			this.Command.ProxyStateChanged += command_ProxyStateChanged;

			OnUIStateChanged(UIStateFlags.None);
			this.Command.DoInitialSetup();

			// UI state must be updated after the initial setup
			// otherwise another window can be opened from the context menu
			OnUIStateChanged(GetUIState());

			return;
		}

		protected override void OnExit(ExitEventArgs e) {
			// process this class level tasks
			StopProxy();
			this.Command.ProxyStateChanged -= command_ProxyStateChanged;
			Util.DisposeWithoutFail(ref this.notifyIcon);

			// process the base class level tasks
			base.OnExit(e);
		}

		#endregion


		#region privates

		private UIStateFlags GetUIState() {
			// base state
			UIStateFlags state = UIStateFlags.ExitEnabled;

			// reflect proxy state
			if (this.IsProxyRunning) {
				state |= UIStateFlags.StopEnabled;
			} else {
				state |= UIStateFlags.StartEnabled;
			}

			// reflect main window state
			MainWindow mainWindow = this.mainWindow;
			if (mainWindow == null) {
				state |= UIStateFlags.SettingsEnabled;
				state |= UIStateFlags.AboutEnabled;
			} else {
				MainWindow.UIStateFlags mainWindowUIState = mainWindow.UIState;
				if ((mainWindowUIState & GUI.MainWindow.UIStateFlags.SettingsEnabled) != 0) {
					state |= UIStateFlags.SettingsEnabled;
				}
				if ((mainWindowUIState & GUI.MainWindow.UIStateFlags.AboutEnabled) != 0) {
					state |= UIStateFlags.AboutEnabled;
				}
			}

			return state;
		}

		private void UpdateUIState() {
			UIStateFlags newState = GetUIState();
			if (newState != this.UIState) {
				this.UIState = newState;
				OnUIStateChanged(newState);
			}

			return;
		}

		private void OnUIStateChanged(UIStateFlags newState) {
			// update state of UI elements
			NotifyIconComponent notifyIcon = this.notifyIcon;
			if (notifyIcon != null) {
				notifyIcon.ExitMenuItem.Enabled = ((newState & UIStateFlags.ExitEnabled) != 0);
				notifyIcon.StartMenuItem.Enabled = ((newState & UIStateFlags.StartEnabled) != 0);
				notifyIcon.StopMenuItem.Enabled = ((newState & UIStateFlags.StopEnabled) != 0);
				notifyIcon.SettingsMenuItem.Enabled = ((newState & UIStateFlags.SettingsEnabled) != 0);
				notifyIcon.AboutMenuItem.Enabled = ((newState & UIStateFlags.AboutEnabled) != 0);

				if (this.Command.IsProxyRunning) {
					notifyIcon.Icon = AssemblyResources.OnIcon;
				} else {
					notifyIcon.Icon = AssemblyResources.OffIcon;
				}
			}

			// notify
			if (this.UIStateChanged != null) {
				try {
					this.UIStateChanged(this, EventArgs.Empty);
				} catch {
					// continue
				}
			}

			return;
		}

		#endregion


		#region event handlers

		private void StartMenuItem_Click(object sender, EventArgs e) {
			try {
				StartProxy();
			} catch (Exception exception) {
				ErrorMessage(exception.Message);
			}
		}

		private void StopMenuItem_Click(object sender, EventArgs e) {
			try {
				StopProxy();
			} catch (Exception exception) {
				ErrorMessage(exception.Message);
			}
		}

		private void OpenMenuItem_Click(object sender, EventArgs e) {
			try {
				OpenMainWindow();
			} catch (Exception exception) {
				ErrorMessage(exception.Message);
			}
		}

		private void SettingsMenuItem_Click(object sender, EventArgs e) {
			try {
				OpenMainWindow().ShowSettingsWindow();
			} catch (Exception exception) {
				ErrorMessage(exception.Message);
			}
		}

		private void AboutMenuItem_Click(object sender, EventArgs e) {
			try {
				OpenMainWindow().ShowAboutWindow();
			} catch (Exception exception) {
				ErrorMessage(exception.Message);
			}
		}

		private void ExitMenuItem_Click(object sender, EventArgs e) {
			Shutdown();
		}

		private void mainWindow_Closed(object sender, EventArgs e) {
			this.mainWindow = null;
		}

		private void mainWindow_UIStateChanged(object sender, EventArgs e) {
			UpdateUIState();
		}

		// Note that this method may be called from non-GUI thread.
		private void command_ProxyStateChanged(object sender, EventArgs e) {
			this.Dispatcher.Invoke(() => { UpdateUIState(); });
		}

		#endregion
	}
}
