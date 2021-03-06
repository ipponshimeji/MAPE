﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MAPE.Utils;
using MAPE.Server;
using MAPE.Server.Settings;
using MAPE.Command.Settings;
using MAPE.Windows.GUI.Settings;
using System.ComponentModel;


namespace MAPE.Windows.GUI {
	public partial class MainWindow: Window {
		#region types

		[Flags]
		public enum UIStateFlags {
			SettingsEnabled = 0x01,
			CloseEnabled = 0x02,
			StartEnabled = 0x04,
			StopEnabled = 0x08,
			ClearEnabled = 0x10,
			HelptopicEnabled = 0x20,
			AboutEnabled = 0x40,

			Invariable = ClearEnabled | HelptopicEnabled,
			InitialState = Invariable | SettingsEnabled | CloseEnabled | StartEnabled | AboutEnabled,
		}

		public class LogMonitor: ILogMonitor {
			#region data

			private readonly MainWindow owner;

			#endregion


			#region creation and disposal

			public LogMonitor(MainWindow owner) {
				// argument checks
				Debug.Assert(owner != null);

				// initialize components
				this.owner = owner;

				return;
			}

			#endregion


			#region ILogMonitor

			public void OnLog(LogEntry entry) {
				this.owner.QueueLog(entry);
			}

			#endregion
		}

		#endregion


		#region data

		private static Brush OnBrush = Brushes.Lime;

		private static Brush OffBrush = Brushes.DarkGray;


		private readonly App app;

		private readonly MainWindowSettings settings;

		internal UIStateFlags UIState { get; private set; }

		private LogMonitor logMonitor;

		private Tuple<MenuItem, TraceLevel>[] logLevelMenuItemGroup;

		private int maxLogCount;

		private SettingsWindow settingsWindow;

		private AboutWindow aboutWindow;

		private bool showingSetupWindow = false;

		#endregion


		#region data - synchronized by locking logQueueLocker

		private object logQueueLocker = new object();

		private Queue<LogEntry> logQueue = new Queue<LogEntry>();

		private bool processing = false;

		#endregion


		#region properties

		private Command Command {
			get {
				return this.app.Command;
			}
		}

		#endregion


		#region events

		public event EventHandler UIStateChanged = null;

		#endregion


		#region creation and disposal

		public MainWindow(App app, MainWindowSettings settings) {
			// argument checks
			if (app == null) {
				throw new ArgumentNullException(nameof(app));
			}
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}

			// initialize members
			this.app = app;
			this.settings = settings;
			this.UIState = UIStateFlags.InitialState;
			this.logMonitor = new LogMonitor(this);
			this.logLevelMenuItemGroup = null;
			this.maxLogCount = 300;
			this.settingsWindow = null;
			this.aboutWindow = null;

			// initialize components
			InitializeComponent();

			GUIForWindowsGUISettings guiSettings = this.Command.GUISettings;
			this.logLevelMenuItemGroup = new Tuple<MenuItem, TraceLevel>[] {
				new Tuple<MenuItem, TraceLevel>(this.offMenuItem, TraceLevel.Off),
				new Tuple<MenuItem, TraceLevel>(this.errorMenuItem, TraceLevel.Error),
				new Tuple<MenuItem, TraceLevel>(this.warningMenuItem, TraceLevel.Warning),
				new Tuple<MenuItem, TraceLevel>(this.infoMenuItem, TraceLevel.Info),
				new Tuple<MenuItem, TraceLevel>(this.verboseMenuItem, TraceLevel.Verbose)
			};
			UpdateLogLevelUI(Logger.LogLevel);

			this.chaseLastLogMenuItem.IsChecked = guiSettings.ChaseLastLog;

			this.app.UIStateChanged += app_UIStateChanged;
			UpdateUIState();
			Logger.AddLogMonitor(this.logMonitor);

			return;
		}

		#endregion


		#region methods

		internal int ShowSetupWindow(SetupContextForWindows setupContext) {
			// argument checks
			Debug.Assert(setupContext != null);

			// state checks
			Debug.Assert(this.Command.IsProxyRunning == false);

			// open the setup window as dialog
			int currentLevel = setupContext.Settings.InitialSetupLevel;
			bool result = false;
			this.showingSetupWindow = true;
			try {
				SetupWindow window = new SetupWindow(this.Command, setupContext);
				window.Owner = this;
				result = (window.ShowDialog() ?? false);
			} finally {
				this.showingSetupWindow = false;
			}

			return result? SetupContextForWindows.LatestInitialSetupLevel: currentLevel;
		}

		internal void ShowSettingsWindow() {
			// state checks
			if ((this.UIState & UIStateFlags.SettingsEnabled) == 0) {
				// currently not enabled
				// maybe a queued event is dispatched belatedly 
				return;
			}

			// show the settings window
			SettingsWindow window = this.settingsWindow;
			if (window != null) {
				window.Activate();
			} else {
				// state checks
				Debug.Assert((this.UIState & UIStateFlags.SettingsEnabled) != 0);

				// open the settings window as dialog
				Command command = this.Command;
				CommandForWindowsGUISettings settings = Command.CloneSettings(command.Settings);
				settings.LogLevel = Logger.LogLevel;	// LogLevel might be changed by LogLevel menu
				window = new SettingsWindow(settings, command.HasSettingsFile, command.IsProxyRunning);
				window.Owner = this;
				this.settingsWindow = window;
				try {
					UpdateUIState();
					if (window.ShowDialog() ?? false) {
						command.SetSettings(window.CommandSettings, window.SaveAsDefault);
						UpdateLogLevelUI(window.CommandSettings.LogLevel);
					}
				} finally {
					this.settingsWindow = null;
					UpdateUIState();
				}
			}
		}

		internal void ShowAboutWindow() {
			// state checks
			if ((this.UIState & UIStateFlags.AboutEnabled) == 0) {
				// currently not enabled
				// maybe a queued event is dispatched belatedly 
				return;
			}

			// show the about window
			AboutWindow window = this.aboutWindow;
			if (window != null) {
				window.Activate();
			} else {
				// state checks
				Debug.Assert((this.UIState & UIStateFlags.AboutEnabled) != 0);

				// open about window as dialog
				window = new AboutWindow();
				window.Owner = this;
				this.aboutWindow = window;
				try {
					UpdateUIState();
					window.ShowDialog();
				} finally {
					this.aboutWindow = null;
					UpdateUIState();
				}
			}
		}

		internal void ErrorMessage(string message) {
			MessageBox.Show(message, this.Command.ComponentName, MessageBoxButton.OK, MessageBoxImage.Error);
		}

		#endregion


		#region overrides

		protected override void OnSourceInitialized(EventArgs e) {
			// perform the base class level tasks
			base.OnSourceInitialized(e);

			// perform this class level tasks
			try {
				RestoreLayout();
			} catch {
				// continue;
			}

			return;
		}

		protected override void OnClosing(CancelEventArgs e) {
			// perform this class level task
			try {
				// save window placement
				SaveLayout();
			} catch {
				// continue
			}

			// perform the base class level task
			base.OnClosing(e);
		}

		protected override void OnClosed(EventArgs e) {
			// clean up this class level
			Logger.RemoveLogMonitor(this.logMonitor);
			this.app.UIStateChanged -= app_UIStateChanged;

			// clean up the base class level
			base.OnClosed(e);
		}

		#endregion


		#region privates

		private void SetUIState(UIStateFlags newState) {
			if (newState != this.UIState) {
				this.UIState = newState;
				OnUIStateChanged(newState);
			}

			return;
		}

		private void UpdateUIState(bool forceSet = false) {
			SetUIState(DetectUIState());
			return;
		}

		private UIStateFlags DetectUIState() {
			// base state
			UIStateFlags state = UIStateFlags.Invariable;
			if (this.showingSetupWindow) {
				return state;
			}

			// reflect dialog state
			if (this.settingsWindow == null && this.aboutWindow == null) {
				state |= (UIStateFlags.SettingsEnabled | UIStateFlags.CloseEnabled | UIStateFlags.AboutEnabled);
			} else {
				if (this.settingsWindow != null) {
					state |= UIStateFlags.SettingsEnabled;
				}
				if (this.aboutWindow != null) {
					state |= UIStateFlags.AboutEnabled;
				}
			}

			// reflect proxy state
			if (this.app.IsProxyRunning) {
				state |= UIStateFlags.StopEnabled;
			} else {
				state |= UIStateFlags.StartEnabled;
			}

			return state;
		}

		private void OnUIStateChanged(UIStateFlags newState) {
			// update state of UI elements
			this.settingsMenuItem.IsEnabled = ((newState & UIStateFlags.SettingsEnabled) != 0);
			this.closeMenuItem.IsEnabled = ((newState & UIStateFlags.CloseEnabled) != 0);
			this.startMenuItem.IsEnabled = ((newState & UIStateFlags.StartEnabled) != 0);
			this.aboutMenuItem.IsEnabled = ((newState & UIStateFlags.AboutEnabled) != 0);
			if ((newState & UIStateFlags.StopEnabled) != 0) {
				// proxy is running
				this.Icon = this.app.OnIcon;
				this.lampEllipse.Fill = OnBrush;
				this.stopMenuItem.IsEnabled = true;
				this.proxyToggleButton.IsChecked = true;
				this.proxyInfoLabel.Content = GetProxyInfo();
			} else {
				// proxy is not running
				this.Icon = this.app.OffIcon;
				this.lampEllipse.Fill = OffBrush;
				this.stopMenuItem.IsEnabled = false;
				this.proxyToggleButton.IsChecked = false;
				this.proxyInfoLabel.Content = string.Empty;
			}

			// invariables
			Debug.Assert((newState & UIStateFlags.Invariable) == UIStateFlags.Invariable);

			// fire event
			if (this.UIStateChanged != null) {
				this.UIStateChanged(this, EventArgs.Empty);
			}

			return;
		}

		private void RestoreLayout() {
			MainWindowSettings settings = this.settings;

			// placement of this window
			RestoreWindowPlacement(settings.Placement);

			// column widths of logListView
			RestoreLogListViewColumnWidths(settings.LogListViewColumnWidths);

			return;
		}

		private void SaveLayout() {
			MainWindowSettings settings = this.settings;
			bool dirty = false;

			// column widths of logListView
			double[] logListViewColumnWidths = GetLogListViewColumnWidths();
			if (logListViewColumnWidths != null) {
				if (settings.LogListViewColumnWidths == null || logListViewColumnWidths.SequenceEqual(settings.LogListViewColumnWidths) == false) {
					settings.LogListViewColumnWidths = logListViewColumnWidths;
					dirty = true;
				}
			}

			// placement of this window
			NativeMethods.WINDOWPLACEMENT? wp = GetWindowPlacement();
			if (wp != settings.Placement) {
				settings.Placement = wp;
				dirty = true;
			}

			// save if changed
			if (dirty) {
				this.Command.SaveMainWindowSettings(settings);
			}

			return;
		}

		private void RestoreWindowPlacement(NativeMethods.WINDOWPLACEMENT? nwp) {
			if (nwp.HasValue) {
				// restore the placement of this window
				NativeMethods.WINDOWPLACEMENT wp = nwp.Value;
				wp.Length = Marshal.SizeOf(typeof(NativeMethods.WINDOWPLACEMENT));
				wp.Flags = 0;
				wp.ShowCmd = (wp.ShowCmd == NativeMethods.SW_SHOWMINIMIZED ? NativeMethods.SW_SHOWNORMAL : wp.ShowCmd);
				IntPtr hwnd = new WindowInteropHelper(this).Handle;
				NativeMethods.SetWindowPlacement(hwnd, ref wp);
			}

			return;
		}

		private NativeMethods.WINDOWPLACEMENT? GetWindowPlacement() {
			// get placement information of this window from Win32.
			NativeMethods.WINDOWPLACEMENT wp = new NativeMethods.WINDOWPLACEMENT();
			wp.Length = Marshal.SizeOf(typeof(NativeMethods.WINDOWPLACEMENT));
			IntPtr hwnd = new WindowInteropHelper(this).Handle;
			if (NativeMethods.GetWindowPlacement(hwnd, out wp)) {
				return wp;
			} else {
				return null;
			}
		}

		private void RestoreLogListViewColumnWidths(IEnumerable<double> widths) {
			if (widths != null) {
				GridView view = this.logListView.View as GridView;
				if (view != null) {
					var columns = view.Columns;
					int i = 0;
					foreach (double width in widths) {
						if (columns.Count <= i) {
							break;
						}
						columns[i].Width = width;
						++i;
					}
				}
			}

			return;
		}

		private double[] GetLogListViewColumnWidths() {
			GridView view = this.logListView.View as GridView;
			if (view != null) {
				return (from column in view.Columns select column.Width).ToArray();
			} else {
				return null;
			}
		}

		private void UpdateLogLevelUI(TraceLevel level) {
			// set menu item
			foreach (var pair in this.logLevelMenuItemGroup) {
				pair.Item1.IsChecked = (pair.Item2 == level);
			}

			this.levelValueLabel.Content = level.ToString();

			return;
		}

		private string GetProxyInfo() {
			StringBuilder buf = new StringBuilder("listening at ");
			CommandSettings commandSettings = this.Command.Settings;
			ProxySettings proxySettings = commandSettings.Proxy;
			Debug.Assert(proxySettings != null);

			// MainListener
			buf.Append(GetListenerEndpoint(proxySettings.MainListener));

			// AdditionalListeners
			IEnumerable<ListenerSettings> additionalListeners = proxySettings.AdditionalListeners;
			if (additionalListeners != null) {
				foreach (ListenerSettings listener in additionalListeners) {
					buf.Append(", ");
					buf.Append(GetListenerEndpoint(listener));
				}
			}

			return buf.ToString();
		}

		private string GetListenerEndpoint(ListenerSettings listenerSettings) {
			// listenerSettings can be null
			IPEndPoint endPoint = ListenerSettings.GetEndPoint(listenerSettings);

			return $"{endPoint.Address}:{endPoint.Port}";
		}

		private string GetHelpTopicUrl() {
			// ToDo: English Page
			return "https://github.com/ipponshimeji/MAPE/blob/master/Documentation/ja/Index.md";
		}

		private void ProcessLog() {
			ItemCollection items = this.logListView.Items;
			LogEntry entry;
			LogAdapter item = null; 
			do {
				// get a log entry from the log queue
				lock (this.logQueueLocker) {
					if (this.logQueue.Count <= 0) {
						if (item != null && this.chaseLastLogMenuItem.IsChecked) {
							// ensure the added item is visible
							this.logListView.ScrollIntoView(item);
						}
						this.processing = false;
						break;
					}
					entry = logQueue.Dequeue();
				}

				// remove some items if the count reaches the limit
				if (this.maxLogCount <= items.Count) {
					// make 10 rooms at a once
					for (int i = 9; 0 <= i; --i) {
						items.RemoveAt(i);
					}
				}

				// add to the list view
				item = new LogAdapter(entry);
				items.Add(item);
			} while (true);

			return;
		}

		#endregion


		#region private - called from outside UI thread

		private void QueueLog(LogEntry entry) {
			lock (this.logQueueLocker) {
				this.logQueue.Enqueue(entry);
				if (this.processing == false) {
					this.processing = true;
					this.Dispatcher.InvokeAsync(ProcessLog);
				}
			}
		}

		#endregion


		#region event handlers

		private void app_UIStateChanged(object sender, EventArgs e) {
			UpdateUIState();
		}

		private void settingsMenuItem_Click(object sender, RoutedEventArgs e) {
			try {
				ShowSettingsWindow();
			} catch (Exception exception) {
				ErrorMessage(exception.Message);
			}
		}

		private void closeMenuItem_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void startMenuItem_Click(object sender, RoutedEventArgs e) {
			try {
				this.app.StartProxy();
			} catch (Exception exception) {
				ErrorMessage(exception.Message);
			}
		}

		private void stopMenuItem_Click(object sender, RoutedEventArgs e) {
			try {
				this.app.StopProxy();
			} catch (Exception exception) {
				ErrorMessage(exception.Message);
			}
		}

		private void loglevelMenuItem_Click(object sender, RoutedEventArgs e) {
			try {
				// update UI
				TraceLevel level = TraceLevel.Error;
				foreach (var pair in this.logLevelMenuItemGroup) {
					if (pair.Item1 == sender) {
						pair.Item1.IsChecked = true;
						level = pair.Item2;
					} else {
						pair.Item1.IsChecked = false;
					}
				}

				this.levelValueLabel.Content = level.ToString();

				// set log level
				Logger.LogLevel = level;
			} catch (Exception exception) {
				ErrorMessage(exception.Message);
			}
		}

		private void clearMenuItem_Click(object sender, RoutedEventArgs e) {
			this.logListView.Items.Clear();
		}

		private void helptopicMenuItem_Click(object sender, RoutedEventArgs e) {
			try {
				// show the help page in the browser
				Process.Start(GetHelpTopicUrl());
			} catch (Exception exception) {
				ErrorMessage(exception.Message);
			}
		}

		private void aboutMenuItem_Click(object sender, RoutedEventArgs e) {
			try {
				ShowAboutWindow();
			} catch (Exception exception) {
				ErrorMessage(exception.Message);
			}
		}

		private void proxyToggleButton_Click(object sender, RoutedEventArgs e) {
			try {
				if (this.proxyToggleButton.IsChecked ?? false) {
					this.app.StartProxy();
				} else {
					this.app.StopProxy();
				}
			} catch (Exception exception) {
				ErrorMessage(exception.Message);
			}
		}

		#endregion
	}
}
