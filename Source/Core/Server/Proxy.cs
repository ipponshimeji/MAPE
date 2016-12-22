using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using MAPE.ComponentBase;
using MAPE.Configuration;


namespace MAPE.Server {
    public class Proxy: Component {
		#region constants

		public const string ObjectBaseName = "Proxy";

		#endregion


		#region data

		private readonly ComponentFactory componentFactory;

		#endregion


		#region data - synchronized by locking this

		private DnsEndPoint server;

		private CredentialPersistence serverCredentialPersistence;

		private NetworkCredential serverCredential;

		private Func<string, NetworkCredential> credentialCallback;

		private List<Listener> listeners;

		private bool isListening;

		private ConnectionCollection connections;

		#endregion


		#region properties

		public ComponentFactory ComponentFactory {
			get {
				return this.componentFactory;
			}
		}

		public DnsEndPoint Server {
			get {
				return this.server;
			}
			set {
				EnsureNotListeningAndSetProperty(value, ref this.server);
			}
		}

		public NetworkCredential ServerCredential {
			get {
				return this.serverCredential;
			}
			set {
				EnsureNotListeningAndSetProperty(value, ref this.serverCredential);
			}
		}

		public Func<string, NetworkCredential> CredentialCallback {
			get {
				return this.credentialCallback;
			}
			set {
				EnsureNotListeningAndSetProperty(value, ref this.credentialCallback);
			}
		}

		public IReadOnlyList<Listener> Listeners {
			get {
				return this.listeners;
			}
		}

		public bool IsListening {
			get {
				return this.isListening;
			}
		}

		public bool IsDisposed {
			get {
				return this.listeners == null;
			}
		}

		#endregion


		#region creation and disposal

		public Proxy(ProxyConfiguration config = null) {
			// argument checks
			// config can be null

			// initialize members
			this.ObjectName = ObjectBaseName;
			if (config != null) {
				this.componentFactory = config.ComponentFactory;
				this.server = config.Proxy;
				this.serverCredentialPersistence = config.ProxyCredentialPersistence;
				this.serverCredential = (this.serverCredentialPersistence == CredentialPersistence.Persistent) ? config.ProxyCredential : null;
			} else {
				// initialize to default settings
				this.componentFactory = new ComponentFactory();
				this.server = null;
				this.serverCredentialPersistence = CredentialPersistence.Process;
				this.serverCredential = null;
			}
			this.credentialCallback = null;
			this.listeners = new List<Listener>();
			this.isListening = false;
			this.connections = null;

			// add listeners
			// add the main listener
			// If config.MainListener is null, default setting is applied.
			if (config != null) {
				this.listeners.Add(this.componentFactory.CreateListener(this, config.MainListener));
				if (config.AdditionalListeners != null && 0 < config.AdditionalListeners.Length) {
					Listener[] additionalListeners = (
						from listenerConfig in config.AdditionalListeners
						where listenerConfig != null
						select this.componentFactory.CreateListener(this, listenerConfig)
					).ToArray();
					this.listeners.AddRange(additionalListeners);
				}
			} else {
				this.listeners.Add(this.componentFactory.CreateListener(this, null));
			}

			return;
		}

		public override void Dispose() {
			// ensure that it stops listening
			Stop();

			// clear members
			lock (this) {
				Debug.Assert(this.connections == null);
				Debug.Assert(this.isListening == false);
				List<Listener> temp = this.listeners;
				this.listeners = null;
				if (temp != null) {
					temp.ForEach(
						(listener) => {
							try {
								listener.Dispose();
							} catch {
								// continue
							}
						}
					);
					this.listeners.Clear();
					this.listeners = null;
				}
				this.credentialCallback = null;
				this.serverCredential = null;
				this.server = null;
			}

			return;
		}

		#endregion


		#region methods

		public void Start() {
			try {
				lock (this) {
					// state checks
					if (this.isListening) {
						// already started
						return;
					}

					IReadOnlyList<Listener> listeners = this.listeners;
					if (listeners == null || listeners.Count <= 0) {
						throw new ObjectDisposedException(this.ObjectName);
					}
					if (this.listeners.Count <= 0) {
						throw new InvalidOperationException("No listening end point is specified.");
					}
					if (this.Server == null) {
						throw new InvalidOperationException("No server end point is specified.");
					}
					TraceInformation("Starting...");

					// start listeners
					int activeCount = 0;
					Parallel.ForEach(
						listeners,
						(listener) => {
							try {
								listener.Start();
								++activeCount;
							} catch {
								// continue
							}
						}
					);
					if (activeCount <= 0) {
						throw new Exception("No active listener.");
					}

					// update its state
					this.isListening = true;
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
					if (this.isListening == false) {
						// already stopped
						return true;
					}
					TraceInformation("Stopping...");

					// stop listening
					IReadOnlyList<Listener> listeners = this.listeners;
					Debug.Assert(listeners != null);
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

					// log
					this.isListening = false;
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

		/// <summary>
		/// 
		/// </summary>
		/// <remarks>
		/// This method is not thread-safe.
		/// Call this method inside lock(this) scope.
		/// </remarks>
		protected void EnsureNotListening() {
			if (this.isListening) {
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
			// state checks
			Debug.Assert(this.Server != null);

			// ToDo: cache ip address?
			TcpClient server = new TcpClient();
			try {
				server.Connect(this.Server.Host, this.Server.Port);
			} catch {
				server.Close();
				throw;
			}

			return server;
		}

		public byte[] GetProxyCredential(string proxyAuthenticateValue, bool needUpdate) {
			// currently proxyAuthenticateValue is not inspected.
			// This method just returns Basic credentials if it is available
			NetworkCredential credential;
			Func<string, NetworkCredential> credentialCallback;
			lock (this) {
				credential = this.serverCredential;
				credentialCallback = this.credentialCallback;
			}

			// query credential if necessary
			if (needUpdate || credential == null) {
				string realm = "Proxy"; // ToDo: extract from the proxyAuthenticateValue
				credential = credentialCallback(realm);
				this.serverCredential = credential;
			}

			return (credential == null)? null: CreateBasicAuthorizationCredential(credential);
		}

		#endregion


		#region privates

		private static byte[] CreateBasicAuthorizationCredential(NetworkCredential credential) {
			// ToDo: encoding of key is UTF-8? or I must escape specail/non-ASCII char?
			string raw = string.Concat(credential.UserName, ":", credential.Password);
			string key = Convert.ToBase64String(Encoding.ASCII.GetBytes(raw));

			return Encoding.ASCII.GetBytes($"Proxy-Authorization: Basic {key}");
		}

		#endregion
	}
}
