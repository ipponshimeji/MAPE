using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using MAPE.ComponentBase;
using MAPE.Configuration;


namespace MAPE.Server {
	public class Listener: TaskingComponent {
		#region constants

		public const string ObjectBaseName = "Listener";

		#endregion


		#region data

		private readonly Proxy owner;

		#endregion


		#region data - synchronized by locking this

		private IPEndPoint endPoint;

		private int backlog;

		private TcpListener tcpListener;

		#endregion


		#region properties

		public Proxy Owner {
			get {
				return this.owner;
			}
		}

		public IPEndPoint EndPoint {
			get {
				return this.endPoint;
			}
			set {
				EnsureNotListeningAndSetProperty(value, ref this.endPoint);
			}
		}

		public int Backlog {
			get {
				return this.backlog;
			}
			set {
				EnsureNotListeningAndSetProperty(value, ref this.backlog);
			}
		}

		public bool IsListening {
			get {
				return this.tcpListener != null;
			}
		}

		public bool IsDisposed {
			get {
				return this.endPoint == null;
			}
		}

		#endregion


		#region creation and disposal

		public Listener(Proxy owner, ListenerConfiguration config = null) {
			// argument checks
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}
			// config can be null

			// initialize members
			this.owner = owner;
			if (config != null) {
				this.endPoint = config.EndPoint;
				this.backlog = config.Backlog;
			} else {
				// set default values
				this.endPoint = new IPEndPoint(IPAddress.Loopback, ListenerConfiguration.DefaultPort);
				this.backlog = ListenerConfiguration.DefaultBacklog;
			}
			Debug.Assert(this.endPoint != null);
			this.tcpListener = null;

			this.ObjectName = $"{ObjectBaseName} ({endPoint.ToString()})";

			return;
		}

		public override void Dispose() {
			// stop listening
			Stop();

			// clear the listener
			lock (this) {
				Debug.Assert(this.tcpListener == null);
				this.endPoint = null;
			}

			return;
		}

		#endregion


		#region methods

		public void Start() {
			try {
				lock (this) {
					// state checks
					if (this.IsDisposed) {
						throw new ObjectDisposedException(this.ObjectName);
					}

					Task listeningTask = this.Task;
					if (listeningTask != null) {
						// already listening
						return;
					}

					// log
					bool verbose = IsLogged(TraceEventType.Verbose);
					if (verbose) {
						LogVerbose("Starting...");
					}

					// start listening
					Debug.Assert(this.endPoint != null);
					TcpListener tcpListener = new TcpListener(this.endPoint);
					Debug.Assert(this.tcpListener == null);
					this.tcpListener = tcpListener;
					try {
						listeningTask = new Task(Listen, TaskCreationOptions.LongRunning);
						tcpListener.Start(this.backlog);
						listeningTask.Start();
						this.Task = listeningTask;
					} catch {
						this.tcpListener = null;
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
						Debug.Assert(this.tcpListener == null);
						throw new ObjectDisposedException(this.ObjectName);
					}

					listeningTask = this.Task;
					if (listeningTask == null) {
						// already stopped
						Debug.Assert(this.tcpListener == null);
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


		/// <summary>
		/// 
		/// </summary>
		/// <remarks>
		/// This method is not thread-safe.
		/// Call this method inside lock(this) scope.
		/// </remarks>
		protected void EnsureNotListening() {
			if (this.tcpListener != null) {
				throw new InvalidOperationException("This opeeration is not available while the object is listening.");
			}

			return;
		}

		protected void EnsureNotListeningAndSetProperty<T>(T value, ref T fieldRef) {
			lock (this) {
				// state checks
				// The property cannot be changed while the object is listening. 
				EnsureNotListening();

				fieldRef = value;
			}

			return;
		}

		#endregion


		#region privates

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
			bool verbose = IsLogged(TraceEventType.Verbose);
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
			} catch (Exception) {
				// ToDo: log if not stopping normally
				;
			}

			// update state
			lock (this) {
				this.tcpListener = null;
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
