using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;


namespace MAPE.Core {
	public class Listener: IDisposable {
		#region constants

		public const int DefaultBackLog = 4;

		public const int DefaultTimeout = 3000;     // 3000[ms]

		#endregion


		#region data

		private readonly Proxy owner;

		private TcpListener listener;

		private int backLog;

		private int timeout;

		private Thread listeningThread;

		#endregion


		#region creation and disposal

		public Listener(Proxy owner, IPEndPoint endPoint, int backLog = DefaultBackLog, int timeout = DefaultTimeout) {
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
			if (timeout < 0 && timeout != Timeout.Infinite) {
				throw new ArgumentOutOfRangeException(nameof(timeout));
			}

			// initialize members
			this.owner = owner;
			this.listener = new TcpListener(endPoint);
			this.backLog = backLog;
			this.timeout = timeout;
			this.listeningThread = null;

			return;
		}

		public void Dispose() {
			// stop listening
			Stop();

			// clear the listener
			lock (this) {
				this.listener = null;
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
					throw new ObjectDisposedException(nameof(Listener));
				}
				Thread listeningThread = this.listeningThread;
				if (listeningThread != null) {
					return;
				}

				// start listening
				try {
					listener.Start(this.backLog);
					listeningThread = new Thread(Listen);
					listeningThread.Start();
				} catch {
					listener.Stop();
					throw;
				}
				this.listeningThread = listeningThread;
			}

			return;
		}

		public void Stop() {
			Thread listeningThread;
			int timeout;
			lock (this) {
				// state checks
				TcpListener listener = this.listener;
				if (listener == null) {
					throw new ObjectDisposedException(nameof(Listener));
				}
				listeningThread = this.listeningThread;
				this.listeningThread = null;
				if (listeningThread == null) {
					return;
				}
				timeout = this.timeout;

				// stop listening
				try {
					listener.Stop();
				} catch {
					// continue
				}
			}

			// wait for the termination of the listening thread
			listeningThread.Join(timeout);

			return;
		}

		#endregion


		#region privates

		private void Listen() {
			TcpListener listener;
			lock (this) {
				listener = this.listener;
			}
			if (listener == null) {
				return;
			}

			try {
				do {
					TcpClient client = listener.AcceptTcpClient();
					this.owner.OnAccept(client);
				} while (true);
			} catch (SocketException exception) {
				// ToDo: 
				;
			}

			return;
		}

		#endregion
	}
}
