using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;


namespace MAPE.Core {
    public class Proxy: IDisposable {
		#region data

		private readonly ComponentFactory componentFactory;

		#endregion


		#region data - synchronized by locking this

		private string serverName;

		private int serverPort;

		private Listener[] listeners;

		private ConnectionCollection connections;

		#endregion


		#region properties

		public ComponentFactory ComponentFactory {
			get {
				return this.componentFactory;
			}
		}

		#endregion


		#region creation and disposal

		public Proxy(ComponentFactory componentFactory) {
			// argument checks
			if (componentFactory == null) {
				// create a default one
				componentFactory = new ComponentFactory();
			}

			// initialize members
			// ToDo: give server info from config
			this.componentFactory = componentFactory;
			this.serverName = "localhost";	// ToDo: just for test
			this.serverPort = 8080;			// ToDo: just for test
			this.listeners = null;
			this.connections = null;

			return;
		}

		public virtual void Dispose() {
			// ensure that it stops listening
			Stop();

			// clear members
			lock (this) {
				Debug.Assert(this.connections == null);
				Debug.Assert(this.listeners == null);
				this.serverPort = 0;
				this.serverName = null;
			}

			return;
		}

		#endregion


		#region methods

		public void Start() {
			lock (this) {
				// state checks
				Listener[] listeners = this.listeners;
				if (listeners != null) {
					// already started
					return;
				}

				// create listeners
				listeners = CreateListeners();
				Action<Listener> start = (listener) => {
					try {
						listener.Start();
					} catch (Exception) {
						// ToDo: log
						// continue
					}
				};

				Parallel.ForEach(listeners, start);
				this.listeners = listeners;
			}

			return;
		}

		public bool Stop(int millisecondsTimeout = 0) {
			bool stopConfirmed = false;
			lock (this) {
				// state checks
				Listener[] listeners = this.listeners;
				this.listeners = null;
				if (listeners == null) {
					// already stopped
					return true;
				}

				// stop listening
				Action<Listener> stop = (listener) => {
					try {
						listener.Stop();
					} catch {
						// ToDo: log
						// continue
					}
				};
				Parallel.ForEach(listeners, stop);

				// stop connections
				ConnectionCollection connections = this.connections;
				this.connections = null;
				if (connections != null) {
					connections.StopAll();
				}

				// wait for the completion of the tasks
				// Note that -1 timeout means 'Infinite'.
				if (millisecondsTimeout != 0) {
					List<Task> tasks = new List<Task>();
					tasks.AddRange(TaskingComponent.GetActiveTaskList(listeners));
					if (connections != null) {
						tasks.AddRange(connections.GetActiveTaskList());
					}

					stopConfirmed = Task.WaitAll(tasks.ToArray(), millisecondsTimeout);
				}

				// dispose listeners
				Action<Listener> dispose = (listener) => {
					try {
						listener.Dispose();
					} catch {
						// continue
					}
				};
				Parallel.ForEach(listeners, dispose);
			}

			return stopConfirmed;
		}

		#endregion


		#region methods - for derived class

		protected void EnsureNotWorking() {
			if (this.listeners != null) {
				throw new InvalidOperationException();
			}
			return;
		}

		#endregion


		#region methods - for Listener objects

		public void OnAccept(TcpClient client) {
			// argument checks
			if (client == null) {
				throw new ArgumentNullException(nameof(client));
			}

			// create a session
			lock (this) {
				// create a ConnectionCollection object if not created
				ConnectionCollection connections = this.connections;
				if (connections == null) {
					connections = this.ComponentFactory.CreateConnectionCollection(this);
					this.connections = connections;
				}

				// add a session
				connections.CreateConnection(client);
			}

			return;
		}

		#endregion


		#region methods - for Connection objects

		public TcpClient OpenServerConnection(TcpClient client) {
			// ToDo: review
			return new TcpClient(this.serverName, this.serverPort);
		}

		#endregion


		#region privates

		private Listener[] CreateListeners() {
			// create listeners
			// ToDo: create Listeners from config
			Listener[] listeners = new Listener[] {
				this.componentFactory.CreateListener(this, new IPEndPoint(IPAddress.Loopback, 8888))
			};

			return listeners;
		}

		#endregion
	}
}
