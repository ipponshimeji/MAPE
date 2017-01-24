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
using System.Windows.Navigation;
using System.Windows.Shapes;
using MAPE.Utils;


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

			public void OnLog(Log log) {
				this.owner.QueueLog(log);
			}

			#endregion
		}

		#endregion


		#region data

		private App app;

		internal UIStateFlags UIState { get; private set; }

		private LogMonitor logMonitor;

		private int maxLogCount;

		private SettingsWindow settingsWindow;

		private AboutWindow aboutWindow;

		#endregion


		#region data - synchronized by locking logQueueLocker

		private object logQueueLocker = new object();

		private Queue<Log> logQueue = new Queue<Log>();

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

		public MainWindow(App app) {
			// argument checks
			if (app == null) {
				throw new ArgumentNullException(nameof(app));
			}

			// initialize members
			this.app = app;
			this.UIState = UIStateFlags.InitialState;
			this.logMonitor = new LogMonitor(this);
			this.maxLogCount = 300;
			this.settingsWindow = null;
			this.aboutWindow = null;

			// initialize components
			InitializeComponent();
		}

		#endregion


		#region methods

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
				window = new SettingsWindow();
				this.settingsWindow = window;
				try {
					UpdateUIState();
					window.ShowDialog();
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

		protected override void OnInitialized(EventArgs e) {
			// initialize the base class level
			base.OnInitialized(e);

			// initialize this class level
			this.app.UIStateChanged += app_UIStateChanged;
			OnUIStateChanged(this.UIState);
			Logger.AddLogMonitor(this.logMonitor);
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

		private UIStateFlags GetUIState() {
			// base state
			UIStateFlags state = UIStateFlags.Invariable;

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
			this.settingsMenuItem.IsEnabled = ((newState & UIStateFlags.SettingsEnabled) != 0);
			this.closeMenuItem.IsEnabled = ((newState & UIStateFlags.CloseEnabled) != 0);
			this.startMenuItem.IsEnabled = ((newState & UIStateFlags.StartEnabled) != 0);
			this.aboutMenuItem.IsEnabled = ((newState & UIStateFlags.AboutEnabled) != 0);
			if ((newState & UIStateFlags.StopEnabled) != 0) {
				// proxy is running
				this.Icon = this.app.OnIcon;
				this.stopMenuItem.IsEnabled = true;
				this.proxyToggleButton.IsChecked = true;
			} else {
				// proxy is not running
				this.Icon = this.app.OffIcon;
				this.stopMenuItem.IsEnabled = false;
				this.proxyToggleButton.IsChecked = false;
			}

			// invariables
			Debug.Assert((newState & UIStateFlags.Invariable) == UIStateFlags.Invariable);

			// fire event
			if (this.UIStateChanged != null) {
				this.UIStateChanged(this, EventArgs.Empty);
			}

			return;
		}

		private string GetHelpTopicUrl() {
			// ToDo: English Page
			return "https://github.com/ipponshimeji/MAPE/blob/master/Documentation/ja/Index.md";
		}

		private void ProcessLog() {
			ItemCollection items = this.logListView.Items;
			Log log;
			do {
				// get a log from the log queue
				lock (this.logQueueLocker) {
					if (this.logQueue.Count <= 0) {
						this.processing = false;
						break;
					}
					log = logQueue.Dequeue();
				}

				// remove some items if the count reaches the limit
				if (this.maxLogCount <= items.Count) {
					// make 10 rooms at a once
					for (int i = 9; 0 <= i; --i) {
						items.RemoveAt(i);
					}
				}

				// add to the list view
				LogAdapter item = new LogAdapter(log);
				items.Add(item);
				// ensure the added item is visible
				this.logListView.ScrollIntoView(item);
			} while (true);

			return;
		}

		#endregion


		#region private - called from outside UI thread

		private void QueueLog(Log log) {
			lock (this.logQueueLocker) {
				this.logQueue.Enqueue(log);
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
