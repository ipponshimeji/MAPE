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
			if (omitDefault == false || mainListener.IsDefault == false) {
				settings.SetObjectValue(Proxy.SettingNames.MainListener, mainListener.GetSettings(omitDefault));
			}

			// AdditionalListeners 
			Settings[] additionalListeners = value.GetRange(1, value.Count - 1).Select(l => l.GetSettings(omitDefault)).ToArray();
			if (omitDefault == false || 0 < additionalListeners.Length) {
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

		public class RevisedBytes {
			#region data

			public readonly int Revision;

			public readonly IReadOnlyCollection<byte> Bytes;

			#endregion


			#region creation and disposal

			public RevisedBytes(int revision, IReadOnlyCollection<byte> bytes) {
				// argument checks
				if (bytes == null) {
					throw new ArgumentNullException(nameof(bytes));
				}

				// initialize members
				this.Bytes = bytes;
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

		private readonly ComponentFactory componentFactory;

		#endregion


		#region data - synchronized by locking this

		private List<Listener> listeners = new List<Listener>();

		private IWebProxy actualProxy;

		private bool keepServerCredential;

		private NetworkCredential serverCredential;

		private RevisedBytes serverBasicCredential;

		private int retryCount;

		private ConnectionCollection connections;

		protected IProxyRunner Runner { get; private set; }

		#endregion


		#region properties

		public ComponentFactory ComponentFactory {
			get {
				return this.componentFactory;
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

		/// <summary>
		/// Whether the Proxy object keeps the server credential or not.
		/// If this value is false, the given credential is kept only during the http session
		/// which requires the credential.
		/// </summary>
		public bool KeepServerCredential {
			get {
				return this.keepServerCredential;
			}
			set {
				lock (this) {
					// state checks
					// this property can be changed during listening

					this.keepServerCredential = value;
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

		public Proxy(ComponentFactory componentFactory, Settings settings) {
			// argument checks
			if (componentFactory == null) {
				componentFactory = new ComponentFactory();
			}

			// initialize members
			this.ObjectName = ObjectBaseName;
			this.componentFactory = componentFactory;

			// listeners
			this.listeners = settings.GetListeners(this);

			// actualProxy
			this.actualProxy = null;

			// serverCredential and 
			this.keepServerCredential = false;
			this.serverCredential = null;
			this.serverBasicCredential = null;

			// retryCount
			this.retryCount = settings.GetInt32Value(SettingNames.RetryCount, defaultValue: DefaultRetryCount);
			// ToDo: value checks

			// misc.
			this.connections = null;
			this.Runner = null;

			return;
		}

		public override void Dispose() {
			// ensure that it stops listening
			Stop();

			// clear members
			lock (this) {
				Debug.Assert(this.Runner == null);
				Debug.Assert(this.connections == null);
				this.serverBasicCredential = null;
				this.serverCredential = null;
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
								LogVerbose($"Fail to dispose {listener.ObjectName}.");
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
						throw new ObjectDisposedException(this.ObjectName);
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
					bool verbose = IsLogged(TraceEventType.Verbose);
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
					bool verbose = IsLogged(TraceEventType.Verbose);
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
				throw new ObjectDisposedException(this.ObjectName);
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

		public RevisedBytes GetProxyBasicCredentials(string realm, RevisedBytes oldBasicCredentials) {
			RevisedBytes basicCredential;
			lock (this) {
				basicCredential = this.serverBasicCredential; 
				NetworkCredential serverCredential = this.serverCredential;

				// need a new credential?
				bool needGetCredential;
				if (serverCredential == null) {
					Debug.Assert(basicCredential == null);
					needGetCredential = true;
				} else {
					Debug.Assert(basicCredential != null);
					// try the current credential if the current revision is newer than the caller's.
					needGetCredential = ((oldBasicCredentials != null) && (basicCredential.Revision <= oldBasicCredentials.Revision));
				}

				// ask its runner to enter new credential 
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

					// ask new credential to the user
					serverCredential = this.Runner.GetCredential(this, realm, needUpdate: (serverCredential != null));
					if (serverCredential == null) {
						// maybe user cancel entering a credential
						basicCredential = null;
					} else {
						basicCredential = new RevisedBytes(revision, CreateBasicProxyAuthorizationBytes(serverCredential));
					}

					// Note that this.keepServerCredential may be changed during the AskCredential() call above,
					// according to user's preference such as "save password?" checkbox.
					if (this.keepServerCredential) {
						this.serverCredential = serverCredential;
						this.serverBasicCredential = basicCredential;
					}
				}
			}

			Debug.Assert(basicCredential != null);
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
