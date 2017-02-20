using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MAPE.Utils;
using MAPE.ComponentBase;
using MAPE.Command;
using SettingNames = MAPE.Server.Proxy.SettingNames;


namespace MAPE.Server {
	public static class ProxySettingsExtensions {
		#region methods

		public static List<Listener> GetListeners(this Settings settings, Proxy proxy) {
			List<Listener> listeners = new List<Listener>();

			// MainListener
			Settings mainListenerSettings = settings.GetObjectValue(Proxy.SettingNames.MainListener);
			Listener listener = proxy.ComponentFactory.CreateListener(proxy, mainListenerSettings);
			listeners.Add(listener);

			// AdditionalListeners
			IEnumerable<Settings> additionalListenerSettings = settings.GetObjectArrayValue(Proxy.SettingNames.AdditionalListeners, defaultValue: null);
			if (additionalListenerSettings != null) {
				Listener[] additionalListeners = (
					from listenerSettings in additionalListenerSettings
					select proxy.ComponentFactory.CreateListener(proxy, listenerSettings)
				).ToArray();
				listeners.AddRange(additionalListeners);
			}

			return listeners;
		}

		public static void SetListeners(this Settings settings, List<Listener> value, bool omitDefault) {
			// argument checks
			if (value == null) {
				throw new ArgumentNullException(nameof(value));
			}
			if (value.Count <= 0) {
				throw new ArgumentException("It must contain at least one item.", nameof(value));
			}

			// MainListener
			Listener mainListener = value[0];
			if (omitDefault && mainListener.IsDefault) {
				settings.RemoveValue(Proxy.SettingNames.MainListener);
			} else {
				settings.SetObjectValue(Proxy.SettingNames.MainListener, mainListener.GetSettings(omitDefault));
			}

			// AdditionalListeners 
			Settings[] additionalListeners = value.GetRange(1, value.Count - 1).Select(l => l.GetSettings(omitDefault)).ToArray();
			if (omitDefault && additionalListeners.Length <= 0) {
				settings.RemoveValue(Proxy.SettingNames.AdditionalListeners);
			} else {
				settings.SetObjectArrayValue(Proxy.SettingNames.AdditionalListeners, additionalListeners);
			}

			return;
		}

		#endregion
	}

	public class Proxy: Component {
		#region types

		public static class SettingNames {
			#region constants

			public const string MainListener = "MainListener";

			public const string AdditionalListeners = "AdditionalListeners";

			public const string RetryCount = "RetryCount";

			#endregion
		}

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
				this.Bytes = bytes;
				this.EnableAssumptionMode = enableAssumptionMode;
				this.Revision = revision;

				return;
			}

			#endregion
		}

		#endregion


		#region constants

		public const string ObjectBaseName = "Proxy";

		public const int DefaultRetryCount = 2;     // original try + 2 retries = 3 tries

		#endregion


		#region data

		private readonly IServerComponentFactory componentFactory;

		#endregion


		#region data - synchronized by locking this

		private List<Listener> listeners = new List<Listener>();

		private IWebProxy actualProxy;

		private int retryCount;

		private ConnectionCollection connections;

		private Dictionary<string, BasicCredential> serverBasicCredentialCache;

		protected IProxyRunner Runner { get; private set; }

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

		public IWebProxy ActualProxy {
			get {
				return this.actualProxy;
			}
			set {
				// argument checks
				// value can be null

				lock (this) {
					// state checks
					EnsureNotListening();

					this.actualProxy = value;
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

		public Proxy(IServerComponentFactory componentFactory, Settings settings) {
			// argument checks
			if (componentFactory == null) {
				componentFactory = new ComponentFactory();
			}

			// initialize members
			this.ComponentName = ObjectBaseName;
			this.componentFactory = componentFactory;

			// listeners
			this.listeners = settings.GetListeners(this);

			// actualProxy
			this.actualProxy = null;

			// retryCount
			this.retryCount = settings.GetInt32Value(SettingNames.RetryCount, defaultValue: DefaultRetryCount);
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
				Debug.Assert(this.Runner == null);
				this.serverBasicCredentialCache.Clear();
				this.serverBasicCredentialCache = null;
				Debug.Assert(this.connections == null);
				this.actualProxy = null;
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

		public BasicCredential GetServerBasicCredentials(string endPoint, string realm, bool firstRequest, BasicCredential oldBasicCredentials) {
			// argument checks
			if (endPoint == null) {
				throw new ArgumentNullException(nameof(endPoint));
			}
			// realm can be null
			// oldBasicCredentials can be null

			BasicCredential basicCredential;
			lock (this) {
				// state checks
				IDictionary<string, BasicCredential> basicCredentialCache = this.serverBasicCredentialCache;
				if (basicCredentialCache == null) {
					throw new ObjectDisposedException(this.ComponentName);
				}

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
					CredentialInfo credential = this.Runner.GetCredential(endPoint, realm, needUpdate: (basicCredential != null));
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


		#region overrides

		public override void AddSettings(Settings settings, bool omitDefault) {
			// argument checks
			Debug.Assert(settings.IsNull == false);

			// state checks
			Debug.Assert(this.IsDisposed == false);
			Debug.Assert(this.listeners != null);

			// MainListener, AdditionalListeners
			settings.SetListeners(this.listeners, omitDefault);

			//	RetryCount
			settings.SetInt32Value(SettingNames.RetryCount, this.retryCount, omitDefault, defaultValue: DefaultRetryCount);

			return;
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
