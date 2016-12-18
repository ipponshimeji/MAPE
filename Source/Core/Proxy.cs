using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;


namespace MAPE.Core {
    public class Proxy: Component {
		#region types

		public enum CredentialNecessity {
			IfPossible,
			Necessary,
			NeedToUpdate,
		}

		#endregion


		#region constants

		public const string ObjectBaseName = "Proxy";

		#endregion


		#region data

		private readonly ComponentFactory componentFactory;

		#endregion


		#region data - synchronized by locking this

		private string serverName;

		private int serverPort;

		private byte[] proxyCredential;

		private Listener[] listeners;

		private ConnectionCollection connections;


		public Func<string, NetworkCredential> CredentialCallback {
			get;
			set;
		} = null;

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
			this.ObjectName = ObjectBaseName;
			// ToDo: give server info from config
			this.componentFactory = componentFactory;
			this.serverName = "localhost";	// ToDo: just for test
			this.serverPort = 8080;         // ToDo: just for test
			this.proxyCredential = null;
			this.listeners = null;
			this.connections = null;

			return;
		}

		public override void Dispose() {
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
			try {
				lock (this) {
					// state checks
					Listener[] listeners = this.listeners;
					if (listeners != null) {
						// already started
						return;
					}
					TraceInformation("Starting...");

					// create listeners
					listeners = CreateListeners();

					// start listeners
					Parallel.ForEach(
						listeners,
						(listener) => {
							try {
								listener.Start();
							} catch {
								// continue
							}
						}
					);

					// update its state
					this.listeners = listeners;
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
				lock (this) {
					// state checks
					Listener[] listeners = this.listeners;
					this.listeners = null;
					if (listeners == null) {
						// already stopped
						return true;
					}
					TraceInformation("Stopping...");

					// stop listening
					Parallel.ForEach(
						listeners,
						(listener) => {
							try {
								listener.Stop();
							} catch {
								// continue
							}
						}
					);

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
					Parallel.ForEach(
						listeners,
						(listener) => {
							try {
								listener.Dispose();
							} catch {
								// continue
							}
						}
					);

					// log
					TraceInformation(stopConfirmed ? "Stopped." : "Requested to stop, but did not comfirm actual stop.");
				}
			} catch (Exception exception) {
				TraceError($"Fail to stop: {exception.Message}");
				throw;
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

		public byte[] GetProxyCredential(string proxyAuthenticateValue, CredentialNecessity necessity) {
			// currently proxyAuthenticateValue is not inspected.
			// This method just returns Basic credentials if it is available
			byte[] credential;
			lock (this) {
				credential = this.proxyCredential;
			}

			switch (necessity) {
				case CredentialNecessity.IfPossible:
					break;
				case CredentialNecessity.Necessary:
					if (credential == null) {
						credential = UpdateProxyCredential("realm");	// ToDo: realm
						// may be still null
					}
					break;
				case CredentialNecessity.NeedToUpdate:
					credential = UpdateProxyCredential("realm");    // ToDo: realm
					// may be still null
					break;
				default:
					credential = null;
					break;
			}

			return credential;
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

		private byte[] UpdateProxyCredential(string realm) {
			// state checks
			Func<string, NetworkCredential> credentialCallback;
			lock (this) {
				credentialCallback = this.CredentialCallback;
			}
			if (credentialCallback == null) {
				return null;
			}

			NetworkCredential credential = credentialCallback(realm);
			if (credential == null) {
				return null;
			}

			return CreateBasicAuthorizationCredential(credential);
		}

		private static byte[] CreateBasicAuthorizationCredential(NetworkCredential credential) {
			// ToDo: encoding of key is UTF-8? or I must escape specail/non-ASCII char?
			string raw = string.Concat(credential.UserName, ":", credential.Password);
			string key = Convert.ToBase64String(Encoding.ASCII.GetBytes(raw));

			return Encoding.ASCII.GetBytes($"Proxy-Authorization: Basic {key}");
		}

		#endregion
	}
}
