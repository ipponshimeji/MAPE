using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace MAPE.Core {
	public class Listener: TaskingComponent {
		#region constants

		public const string ObjectBaseName = "Listener";

		public const int DefaultBackLog = 8;

		#endregion


		#region data

		private readonly Proxy owner;

		#endregion


		#region data - synchronized by locking this

		private TcpListener listener;

		private int backLog;

		#endregion


		#region creation and disposal

		public Listener(Proxy owner, IPEndPoint endPoint, int backLog = DefaultBackLog) {
			// argument checks
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}
			if (endPoint == null) {
				throw new ArgumentNullException(nameof(endPoint));
			}
			if (backLog < 0) {
				throw new ArgumentOutOfRangeException(nameof(backLog));
			}

			// initialize members
			this.ObjectName = $"{ObjectBaseName} ({endPoint.ToString()})";
			this.owner = owner;
			this.listener = new TcpListener(endPoint);
			this.backLog = backLog;

			return;
		}

		public override void Dispose() {
			// stop listening
			Stop();

			// clear the listener
			lock (this) {
				this.listener = null;
				// listeningTask will be cleard at this.Task
				// see the prop getter of ListeningTask
			}

			return;
		}

		#endregion


		#region methods

		public void Start() {
			try {
				lock (this) {
					// state checks
					TcpListener listener = this.listener;
					if (listener == null) {
						throw new ObjectDisposedException(this.ObjectName);
					}

					Task listeningTask = this.Task;
					if (listeningTask != null) {
						// already listening
						return;
					}
					TraceInformation("Starting...");

					// start listening
					try {
						listeningTask = new Task(Listen, TaskCreationOptions.LongRunning);
						listener.Start(this.backLog);
						listeningTask.Start();
					} catch {
						listener.Stop();
						throw;
					}

					// update its state
					this.Task = listeningTask;
					TraceInformation("Started.");
				}
			} catch (Exception exception) {
				TraceError($"Fail to start: {exception.Message}");
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
					TcpListener listener = this.listener;
					if (listener == null) {
						throw new ObjectDisposedException(this.ObjectName);
					}

					listeningTask = this.Task;
					if (listeningTask == null) {
						// already stopped
						return true;
					}
					TraceInformation("Stopping...");

					// stop listening
					try {
						listener.Stop();
					} catch {
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
				TraceError($"Fail to stop: {exception.Message}");
				throw;
			}

			return stopConfirmed;
		}

		#endregion


		#region privates

		private void Listen() {
			// state checks
			TcpListener listener;
			Proxy owner;
			lock (this) {
				listener = this.listener;
				owner = this.owner;
			}
			if (listener == null) {
				// may be disposed immediately after Start() call
				return;
			}

			// start accept loop
			try {
				do {
					TcpClient client = listener.AcceptTcpClient();
					try {
						TraceInformation($"Accepted from {client.Client.RemoteEndPoint.ToString()}. Creating a Connection.");
						owner.OnAccept(client);
						TraceInformation($"Connection created.");
					} catch (Exception exception) {
						TraceError($"Fail to create a Connection: {exception.Message}");
						// continue
					}
				} while (true);
			} catch (Exception) {
				// ToDo: log if not stopping
				;
			}

			// log
			TraceInformation("Stopped.");

			return;
		}

		#endregion
	}
}
