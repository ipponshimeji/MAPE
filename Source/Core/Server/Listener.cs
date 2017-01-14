using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using MAPE.Utils;
using MAPE.ComponentBase;


namespace MAPE.Server {
	public static class ListenerSettingsExtensions {
		#region methods

		public static IPAddress GetIPAddressValue(this Settings settings, string settingName, IPAddress defaultValue) {
			Settings.Value value = settings.GetValue(settingName);
			if (value.IsNull == false) {
				return IPAddress.Parse(value.GetStringValue());
			} else {
				return defaultValue;
			}
		}

		public static void SetIPAddressValue(this Settings settings, string settingName, IPAddress value, bool omitDefault, IPAddress defaultValue) {
			if (omitDefault && value == defaultValue) {
				settings.RemoveValue(settingName);
			} else {
				string stringValue = (value == null) ? null : value.ToString();
				settings.SetStringValue(settingName, stringValue, omitDefault: false);
			}
		}

		#endregion
	}

	public class Listener: TaskingComponent {
		#region types

		public static class SettingNames {
			#region constants

			public const string Address = "Address";

			public const string Port = "Port";

			public const string Backlog = "Backlog";

			#endregion
		}

		#endregion


		#region constants

		public const string ObjectBaseName = "Listener";

		public const int DefaultPort = 8888;

		public const int DefaultBacklog = 8;

		#endregion


		#region data

		public static readonly IPAddress DefaultAddress = IPAddress.Loopback;


		private readonly Proxy owner;

		#endregion


		#region data - synchronized by locking this

		private TcpListener tcpListener;

		private int backlog;

		#endregion


		#region properties

		public Proxy Owner {
			get {
				return this.owner;
			}
		}

		public IPEndPoint EndPoint {
			get {
				lock (this) {
					// state checks
					if (this.IsDisposed) {
						throw new ObjectDisposedException(this.ComponentName);
					}
					Debug.Assert(this.tcpListener != null);

					return this.tcpListener.LocalEndpoint as IPEndPoint;
				}
			}
			protected set {
				// argument checks
				if (value == null) {
					throw new ArgumentNullException(nameof(value));
				}

				lock (this) {
					// state checks
					if (this.IsDisposed) {
						throw new ObjectDisposedException(this.ComponentName);
					}
					if (this.IsListening) {
						throw new InvalidOperationException();
					}

					SetEndPoint(value);
				}
			}
		}

		public int Backlog {
			get {
				return this.backlog;
			}
		}

		public bool IsDisposed {
			get {
				return this.tcpListener == null;
			}
		}

		public bool IsListening {
			get {
				return this.Task != null;
			}
		}

		public bool IsDefault {
			get {
				IPEndPoint endPoint = this.EndPoint;
				return this.backlog == DefaultBacklog && endPoint.Port == DefaultPort && endPoint.Address == DefaultAddress;
			}
		}

		#endregion


		#region creation and disposal

		public Listener(Proxy owner, Settings settings) {
			// argument checks
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}

			// initialize members
			this.ParentComponentId = owner.ComponentId;
			this.owner = owner;

			// backlog
			this.backlog = settings.GetInt32Value(SettingNames.Backlog, DefaultBacklog);

			// tcpListener			
			IPAddress address = settings.GetIPAddressValue(SettingNames.Address, DefaultAddress);
			int port = settings.GetInt32Value(SettingNames.Port, DefaultPort);
			SetEndPoint(new IPEndPoint(address, port));

			return;
		}

		public override void Dispose() {
			// stop listening
			Stop();

			// clear the listener
			lock (this) {
				this.ComponentName = ObjectBaseName;
				this.tcpListener = null;
			}

			return;
		}

		#endregion


		#region methods - start & stop

		public void Start() {
			try {
				lock (this) {
					// state checks
					if (this.IsDisposed) {
						throw new ObjectDisposedException(this.ComponentName);
					}
					Debug.Assert(this.tcpListener != null);

					Task listeningTask = this.Task;
					if (listeningTask != null) {
						// already listening
						return;
					}

					// log
					bool verbose = ShouldLog(TraceEventType.Verbose);
					if (verbose) {
						LogVerbose("Starting...");
					}

					// start listening
					try {
						listeningTask = new Task(Listen, TaskCreationOptions.LongRunning);
						tcpListener.Start(this.backlog);
						listeningTask.Start();
						this.Task = listeningTask;
					} catch {
						tcpListener.Stop();
						throw;
					}

					// log
					if (verbose) {
						LogVerbose("Started.");
					}
				}
			} catch (Exception exception) {
				LogError($"Fail to start: {exception.Message}");
				throw;
			}

			return;
		}

		public bool Stop(int millisecondsTimeout = 0) {
			bool stopConfirmed = false;
			try {
				Task listeningTask;
				lock (this) {
					// state checks
					if (this.IsDisposed) {
						throw new ObjectDisposedException(this.ComponentName);
					}
					Debug.Assert(this.tcpListener != null);

					listeningTask = this.Task;
					if (listeningTask == null) {
						// already stopped
						return true;
					}

					// log
					LogVerbose("Stopping...");

					// stop listening
					try {
						this.tcpListener.Stop();
					} catch (Exception exception) {
						LogVerbose($"Exception on stopping listener: {exception.Message}");
						// continue
					}
				}

				// wait for the completion of the listening task
				// Note that -1 timeout means 'Infinite'.
				if (millisecondsTimeout != 0) {
					stopConfirmed = listeningTask.Wait(millisecondsTimeout);
				}

				// log
				// "Stopped." will be logged at the last of the listening task. See Listen().
			} catch (Exception exception) {
				LogError($"Fail to stop: {exception.Message}");
				throw;
			}

			return stopConfirmed;
		}

		#endregion


		#region overrides

		public override void AddSettings(Settings settings, bool omitDefault) {
			// argument checks
			Debug.Assert(settings.IsNull == false);

			// Address
			IPEndPoint endPoint = this.EndPoint;
			settings.SetIPAddressValue(SettingNames.Address, endPoint.Address, omitDefault, DefaultAddress);

			// Port
			settings.SetInt32Value(SettingNames.Port, endPoint.Port, omitDefault, DefaultPort);

			// Backlog
			settings.SetInt32Value(SettingNames.Backlog, this.backlog, omitDefault, DefaultBacklog);

			return;
		}

		#endregion


		#region privates

		private void SetEndPoint(IPEndPoint endPoint) {
			// argument checks
			Debug.Assert(endPoint != null);

			// update end point related state
			this.tcpListener = new TcpListener(endPoint);
			this.ComponentName = $"{ObjectBaseName} ({endPoint})";

			return;
		}

		private void Listen() {
			// state checks
			Proxy owner;
			TcpListener tcpListener;
			lock (this) {
				owner = this.owner;
				tcpListener = this.tcpListener;
			}
			if (tcpListener == null) {
				// may be disposed immediately after Start() call
				return;
			}

			// start accept loop
			bool verbose = ShouldLog(TraceEventType.Verbose);
			try {
				do {
					TcpClient client = tcpListener.AcceptTcpClient();
					try {
						if (verbose == false) {
							owner.OnAccept(client);
						} else {
							LogVerbose($"Accepted from {client.Client.RemoteEndPoint.ToString()}. Creating a Connection...");
							owner.OnAccept(client);
							LogVerbose($"Connection created.");
						}
					} catch (Exception exception) {
						LogError($"Fail to create a Connection: {exception.Message}");
						// continue
					}
				} while (true);
			} catch (SocketException exception) {
				if (exception.SocketErrorCode != SocketError.Interrupted) {
					LogError(exception.Message);
				}
			} catch (Exception exception) {
				LogError(exception.Message);
			}

			// log
			if (verbose) {
				LogVerbose("Stopped.");
			}

			return;
		}

		#endregion
	}
}
