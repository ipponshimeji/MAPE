using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace MAPE.Core {
	public class Listener: TaskingComponent, IDisposable {
		#region constants

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
			this.owner = owner;
			this.listener = new TcpListener(endPoint);
			this.backLog = backLog;

			return;
		}

		public void Dispose() {
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
			lock (this) {
				// state checks
				TcpListener listener = this.listener;
				if (listener == null) {
					throw new ObjectDisposedException(this.GetType().Name);
				}

				Task listeningTask = this.Task;
				if (listeningTask != null) {
					// already listening
					return;
				}

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
			}

			return;
		}

		public bool Stop(int millisecondsTimeout = 0) {
			Task listeningTask;
			lock (this) {
				// state checks
				TcpListener listener = this.listener;
				if (listener == null) {
					throw new ObjectDisposedException(nameof(Listener));
				}

				listeningTask = this.Task;
				if (listeningTask == null) {
					// already stopped
					return true;
				}

				// stop listening
				try {
					listener.Stop();
				} catch {
					// continue
				}
			}

			// wait for the completion of the listening task
			// Note that -1 timeout means 'Infinite'.
			bool stopConfirmed = false;
			if (millisecondsTimeout != 0) {
				stopConfirmed = listeningTask.Wait(millisecondsTimeout);
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
					owner.OnAccept(client);
				} while (true);
			} catch (Exception) {
				// ToDo: log
				;
			}

			return;
		}

		#endregion
	}
}
