using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using MAPE.ComponentBase;
using MAPE.Server.Settings;


namespace MAPE.Server {
	public class Listener: TaskingComponent {
		#region constants

		public const string ObjectBaseName = "Listener";

		#endregion


		#region data

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

		#endregion


		#region creation and disposal

		public Listener(Proxy owner, ListenerSettings settings) {
			// argument checks
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}

			// initialize members
			this.ParentComponentId = owner.ComponentId;
			this.owner = owner;

			// backlog
			this.backlog = settings.Backlog;

			// tcpListener			
			IPAddress address = settings.Address;
			int port = settings.Port;
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
