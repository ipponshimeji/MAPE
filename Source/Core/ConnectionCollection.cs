using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace MAPE.Core {
    public class ConnectionCollection {
		#region data

		private readonly Proxy owner;

		#endregion


		#region data - synchronized by locking this

		private readonly List<Connection> connectionList = new List<Connection>();

		#endregion


		#region properties

		public Proxy Owner {
			get {
				return this.owner;
			}
		}

		public ComponentFactory ComponentFactory {
			get {
				return this.owner.ComponentFactory;
			}
		}

		#endregion


		#region creation and disposal

		public ConnectionCollection(Proxy owner) {
			// argument checks
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}

			// initialize members
			this.owner = owner;
			Debug.Assert(this.connectionList != null);

			return;
		}

		#endregion


		#region methods

		public void CreateConnection(TcpClient client) {
			// argument checks
			if (client == null) {
				throw new ArgumentNullException(nameof(client));
			}

			// create a connection object and add it to the connection list
			try {
				Connection connection = this.ComponentFactory.AllocConnection(this);
				try {
					lock (this) {
						this.connectionList.Add(connection);
					}
					connection.StartCommunication(client);
				} catch {
					connection.StopCommunication();
					// do not store the error instance into the cache
					this.ComponentFactory.ReleaseConnection(connection, discardInstance: true);
					throw;
				}
			} catch {
				client.Close();
				throw;
			}

			return;
		}

		public void StopAll() {
			lock (this) {
				List<Connection> connectionList = this.connectionList;
				if (0 < connectionList.Count) {
					Parallel.ForEach(
						connectionList,
						(connection) => {
							try {
								connection.StopCommunication();
							} catch {
								// continue
							}
						}
					);
				}
			}

			return;
		}

		public Task[] GetActiveTaskList() {
			return TaskingComponent.GetActiveTaskList(this.connectionList);
		}

		#endregion


		#region methods - for Connection class only

		internal void OnConnectionCompleted(Connection connection) {
			// argument checks
			if (connection == null) {
				throw new ArgumentNullException(nameof(connection));
			}
			Debug.Assert(connection.Task == null);

			try {
				// remove from the connections
				lock (this) {
					this.connectionList.Remove(connection);
				}

				// store the instance into the cache
				this.ComponentFactory.ReleaseConnection(connection);
			} catch {
				// do not store the error instance into the cache
				this.ComponentFactory.ReleaseConnection(connection, discardInstance: true);
				// continue
			}

			return;
		}

		#endregion
	}
}
