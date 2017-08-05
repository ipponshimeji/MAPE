using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using MAPE.Utils;
using MAPE.ComponentBase;
using MAPE.Command;
using MAPE.Command.Settings;
using MAPE.Server.Settings;


namespace MAPE.Server {
	public class Proxy: Component {
		#region types

		public class BasicCredential {
			#region data

			public readonly int Revision;

			public readonly bool EnableAssumptionMode;

			public readonly IReadOnlyCollection<byte> Bytes;

			#endregion


			#region creation and disposal

			public BasicCredential(int revision, bool enableAssumptionMode, IReadOnlyCollection<byte> bytes) {
				// argument checks
				if (bytes == null) {
					throw new ArgumentNullException(nameof(bytes));
				}

				// initialize members
				this.Revision = revision;
				this.EnableAssumptionMode = enableAssumptionMode;
				this.Bytes = bytes;

				return;
			}

			#endregion
		}

		#endregion


		#region constants

		public const string ObjectBaseName = "Proxy";

		#endregion


		#region data

		private readonly IServerComponentFactory componentFactory;

		#endregion


		#region data - synchronized by locking this

		private List<Listener> listeners = new List<Listener>();

		private IActualProxy actualProxy;

		private int retryCount;

		private ConnectionCollection connections;

		protected IProxyRunner Runner { get; private set; }

		#endregion


		#region data - synchronized by locking credentialCacheLocker

		private object credentialCacheLocker = new object();

		/// <remarks>
		/// Note that the value of this field itself is synchronized by locking this
		/// while contents of this object are synchronized by locking credentialCacheLocker.
		/// </remarks>
		/// <example>
		/// lock (this) {
		///		this.serverBasicCredentialCache = new Dictionary<string, BasicCredential>();
		/// }
		/// lock (this.credentialCacheLocker) {
		///		this.serverBasicCredentialCache.Clear();
		/// }
		/// </example>
		private Dictionary<string, BasicCredential> serverBasicCredentialCache;

		#endregion


		#region properties

		public IServerComponentFactory ComponentFactory {
			get {
				return this.componentFactory;
			}
		}

		public IPEndPoint MainListenerEndPoint {
			get {
				lock (this) {
					// state checks
					EnsureNotDisposed();
					Debug.Assert(this.listeners != null && 0 < this.listeners.Count);

					return this.listeners[0].EndPoint;
				}
			}
		}

		public IActualProxy ActualProxy {
			get {
				return this.actualProxy;
			}
			set {
				// argument checks
				// value can be null

				lock (this) {
					// state checks
					EnsureNotListening();

					IActualProxy oldValue = this.actualProxy;
					this.actualProxy = value;
					Util.DisposeWithoutFail(oldValue);
				}
			}
		}

		public int RetryCount {
			get {
				return this.retryCount;
			}
			set {
				lock (this) {
					// state checks
					// retryCount can be changed during listening

					this.retryCount = value;
				}
			}
		}

		public bool IsListening {
			get {
				return this.Runner != null;
			}
		}

		public bool IsDisposed {
			get {
				return this.listeners == null;
			}
		}

		#endregion


		#region creation and disposal

		public Proxy(IServerComponentFactory componentFactory, ProxySettings settings) {
			// argument checks
			if (componentFactory == null) {
				throw new ArgumentNullException(nameof(componentFactory));
			}
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}

			// initialize members
			this.ComponentName = ObjectBaseName;
			this.componentFactory = componentFactory;

			// listeners
			// ToDo: can be Listener[]?
			this.listeners = new List<Listener>(settings.GetListeners().Select(
				s => this.componentFactory.CreateListener(this, s)
			));

			// actualProxy
			this.actualProxy = null;

			// retryCount
			this.retryCount = settings.RetryCount;
			// ToDo: value checks

			// misc.
			this.connections = null;
			this.serverBasicCredentialCache = new Dictionary<string, BasicCredential>();
			this.Runner = null;

			return;
		}

		public override void Dispose() {
			// ensure that it stops listening
			Stop();

			// clear members
			lock (this) {
				this.serverBasicCredentialCache = null;
				Debug.Assert(this.Runner == null);
				Debug.Assert(this.connections == null);
				Util.DisposeWithoutFail(ref this.actualProxy);
				List<Listener> temp = this.listeners;
				this.listeners = null;
				if (temp != null) {
					temp.ForEach(
						(listener) => {
							try {
								listener.Dispose();
							} catch {
								// continue
								LogVerbose($"Fail to dispose {listener.ComponentName}.");
							}
						}
					);
					temp.Clear();
				}
			}

			return;
		}

		#endregion


		#region methods

		public void Start(IProxyRunner runner) {
			// argument checks
			if (runner == null) {
				throw new ArgumentNullException(nameof(runner));
			}

			try {
				lock (this) {
					// state checks
					if (this.IsDisposed) {
						throw new ObjectDisposedException(this.ComponentName);
					}
					IReadOnlyList<Listener> listeners = this.listeners;
					Debug.Assert(listeners != null);

					if (this.IsListening) {
						// already started
						return;
					}

					if (listeners.Count <= 0) {
						throw new InvalidOperationException("No listening end point is specified.");
					}

					// log
					bool verbose = ShouldLog(TraceEventType.Verbose);
					if (verbose) {
						LogVerbose("Starting...");
					}

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
					if (verbose) {
						LogVerbose("Started.");
					}
					this.Runner = runner;
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
				lock (this) {
					// state checks
					if (this.IsListening == false) {
						// already stopped
						return true;
					}
					Debug.Assert(this.IsDisposed == false);

					// log
					bool verbose = ShouldLog(TraceEventType.Verbose);
					if (verbose) {
						LogVerbose("Stopping...");
					}

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

						if (0 < tasks.Count) {
							stopConfirmed = Task.WaitAll(tasks.ToArray(), millisecondsTimeout);
						} else {
							stopConfirmed = true;
						}
					}

					// update its state
					this.Runner = null;
					if (stopConfirmed) {
						if (verbose) {
							LogVerbose("Stopped.");
						}
					} else {
						string message = "Requested to stop, but did not comfirm actual stop.";
						if (millisecondsTimeout == 0) {
							LogVerbose(message);
						} else {
							LogWarning(message);
						}
					}
				}
			} catch (Exception exception) {
				LogError($"Fail to stop: {exception.Message}");
				throw;
			}

			return stopConfirmed;
		}

		#endregion


		#region methods - for derived classes

		// not thread-safe
		public void EnsureNotDisposed() {
			if (this.IsDisposed) {
				throw new ObjectDisposedException(this.ComponentName);
			}
		}

		// not thread-safe
		public void EnsureNotListening() {
			EnsureNotDisposed();
			if (this.IsListening) {
				throw new InvalidOperationException("You cannot perform this operation while the object is listening.");
			}
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
				// state checks
				if (this.IsListening == false) {
					// the proxy stoped just before the listener calling this method
					client.Close();
					return;
				}

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

		/// <remarks>
		/// Note that Dispose() may be called during running this method.
		/// That is, system shutdown or suspend may cause Dispose() call
		/// while it executes runner.GetCredential(), which may be opening a CredentialDialog.
		/// In this case, runner.GetCredential() will return in the next turn
		/// on the GUI thread cycle after Dispose() is called.
		/// </remarks>
		public BasicCredential GetServerBasicCredentials(string endPoint, string realm, bool firstRequest, BasicCredential oldBasicCredentials) {
			// argument checks
			if (endPoint == null) {
				throw new ArgumentNullException(nameof(endPoint));
			}
			// realm can be null
			// oldBasicCredentials can be null

			BasicCredential basicCredential;
			IDictionary<string, BasicCredential> basicCredentialCache;
			IProxyRunner runner;

			// the value of the this.serverBasicCredentialCache field is synchronized by locking this
			lock (this) {
				// state checks
				basicCredentialCache = this.serverBasicCredentialCache;
				if (basicCredentialCache == null) {
					throw new ObjectDisposedException(this.ComponentName);
				}
				runner = this.Runner;
			}

			// the contents of the this.serverBasicCredentialCache are synchronized by locking this.credentialCacheLocker
			lock (this.credentialCacheLocker) {
				// get value from the cache
				basicCredentialCache.TryGetValue(endPoint, out basicCredential);

				// need a new credential?
				bool needGetCredential;
				if (basicCredential == null) {
					needGetCredential = !firstRequest;
				} else {
					// try the current credential if the current revision is newer than the caller's.
					needGetCredential = ((oldBasicCredentials != null) && (basicCredential.Revision <= oldBasicCredentials.Revision));
				}

				// get the credential from the runner 
				if (needGetCredential) {
					// figure out the next revision number 
					int revision = 1;
					if (basicCredential != null) {
						revision = basicCredential.Revision;
						if (int.MaxValue <= revision) {
							throw new Exception("An internal counter was overflowed.");
						}
						++revision;
					}

					// get the credential from the runner
					CredentialSettings credential = null;
					if (runner != null) {
						credential = runner.GetCredential(endPoint, realm, needUpdate: (basicCredential != null));
					}
					if (credential == null) {
						// maybe user cancel entering a credential
						basicCredential = null;
					} else {
						basicCredential = new BasicCredential(revision, credential.EnableAssumptionMode ,CreateBasicProxyAuthorizationBytes(credential.GetNetworkCredential()));
					}

					// update the cache
					if (basicCredential == null || credential.Persistence == CredentialPersistence.Session) {
						basicCredentialCache.Remove(endPoint);
					} else {
						basicCredentialCache[endPoint] = basicCredential;
					}
				}
			}

			// adjust for first time
			if (firstRequest && basicCredential != null && basicCredential.EnableAssumptionMode == false) {
				// In the first request, the basic credential is returned only if the AssumptionMode is enabled 
				basicCredential = null;
			}

			return basicCredential;
		}

		#endregion


		#region privates

		private static byte[] CreateBasicProxyAuthorizationBytes(NetworkCredential credential) {
			// argument checks
			if (credential == null) {
				return null;
			}
			string userName = credential.UserName;
			if (userName == null) {
				userName = string.Empty;
			}
			string password = credential.Password;
			if (password == null) {
				password = string.Empty;
			}

			// ToDo: encoding of key is UTF-8? or I must escape specail/non-ASCII char?
			string raw = string.Concat(userName, ":", password);
			string key = Convert.ToBase64String(Encoding.ASCII.GetBytes(raw));

			return Encoding.ASCII.GetBytes($"Proxy-Authorization: Basic {key}\r\n");
		}

		#endregion
	}
}
