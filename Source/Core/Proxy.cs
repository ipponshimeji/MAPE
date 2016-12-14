using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;


namespace MAPE.Core {
    public class Proxy: IDisposable {
		#region data

		private List<Listener> listeners;

		private List<Session> sessions;

		#endregion


		#region creation and disposal

		public Proxy() {
			// initialize members
			this.listeners = null;
			this.sessions = null;

			return;
		}

		public virtual void Dispose() {
			// clear the cache
			return;
		}

		#endregion


		#region methods

		public void Start() {
			lock (this) {
				// state checks
				List<Listener> listeners = this.listeners;
				if (listeners != null) {
					// already started
					return;
				}

				// create listeners
				listeners = new List<Listener>();
				// ToDo: create Listeners from config
				listeners.Add(new Listener(this, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8888)));
				listeners.ForEach(l => { l.Start(); });
				// ToDo: error handling
				this.listeners = listeners;
			}

			return;
		}

		public void Stop() {
			lock (this) {
				// state checks
				List<Listener> listeners = this.listeners;
				if (listeners == null) {
					// already stopped
					return;
				}

				// create listeners
				listeners.ForEach(l => { l.Stop(); });

				List<Session> sessions = this.sessions;
				if (sessions != null) {
					sessions.ForEach(s => { s.Dispose(); });
				}
				// ToDo: wait for closing
			}

			return;
		}

		#endregion


		#region methods - for Listener

		public void OnAccept(TcpClient client) {
			throw new NotImplementedException();
		}

		#endregion
	}
}
