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


		public static DnsEndPoint GetDnsEndPointValue(this Settings settings, string settingName, DnsEndPoint defaultValue) {
			Settings.Value value = settings.GetValue(settingName);
			if (value.IsNull == false) {
				return value.GetObjectValue().CreateDnsEndPoint();
			} else {
				return defaultValue;
			}
		}

		public static DnsEndPoint CreateDnsEndPoint(this Settings settings) {
			Settings.Value host = settings.GetValue(Proxy.SettingNames.Host);
			Settings.Value port = settings.GetValue(Proxy.SettingNames.Port);
			if (host.IsNull || port.IsNull) {
				throw new FormatException($"Both '{Proxy.SettingNames.Host}' and '{Proxy.SettingNames.Port}' settings are indispensable.");
			}

			return new DnsEndPoint(host.GetStringValue(), port.GetInt32Value());
		}

		public static void SetDnsEndPointValue(this Settings settings, string settingName, DnsEndPoint value, bool omitDefault = false, DnsEndPoint defaultValue = null) {
			// argument checks
			if (settingName == null) {
				throw new ArgumentNullException(nameof(settingName));
			}

			// add a setting if necessary 
			if (omitDefault == false || value != defaultValue) {
				settings.SetObjectValue(settingName, GetDnsEndPointSettings(value, omitDefault));
			}

			return;
		}

		public static Settings GetDnsEndPointSettings(DnsEndPoint value, bool omitDefault) {
			// argument checks
			if (value == null) {
				return Settings.NullSettings;
			}

			// create settings of the DnsEndPoint
			Settings settings = Settings.CreateEmptySettings();

			settings.SetStringValue(Proxy.SettingNames.Host, value.Host);
			settings.SetInt32Value(Proxy.SettingNames.Port, value.Port);

			return settings;
		}

		#endregion
	}

	public class Proxy: Component {
		#region types

		public static class SettingNames {
			#region constants

			public const string MainListener = "MainListener";

			public const string AdditionalListeners = "AdditionalListeners";

			public const string Server = "Server";

			public const string Host = "Host";

			public const string Port = "Port";

			public const string RetryCount = "RetryCount";

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

		private DnsEndPoint server;

		private bool isServerCredentialPersistencyProcess;

		private NetworkCredential serverCredential;

		private byte[] basicServerCredential;

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

		public DnsEndPoint Server {
			get {
				return this.server;
			}
			set {
				// argument checks
				// value can be null

				lock (this) {
					// state checks
					EnsureNotListening();

					this.server = value;
				}
			}
		}

		public bool IsServerCredentialPersistencyProcess {
			get {
				return this.isServerCredentialPersistencyProcess;
			}
			set {
				lock (this) {
					// state checks
					// this property can be changed during listening

					this.isServerCredentialPersistencyProcess = value;
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

			// server
			this.server = settings.GetDnsEndPointValue(SettingNames.Server, null);

			// serverCredential, serverCredentialPersistence, basicServerCredential
			this.serverCredential = null;
			this.isServerCredentialPersistencyProcess = false;
			this.basicServerCredential = null;

			// retryCount
			this.retryCount = settings.GetInt32Value(SettingNames.RetryCount, DefaultRetryCount);
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
				this.basicServerCredential = null;
				this.serverCredential = null;
				this.server = null;
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
					if (this.Server == null) {
						throw new InvalidOperationException("No server end point is specified.");
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

			// preparations
			byte[] basicCredential;
			lock (this) {
				NetworkCredential credential = this.serverCredential;
				basicCredential = this.basicServerCredential;
				IProxyRunner runner = this.Runner;
				Debug.Assert(runner != null);

				if (needUpdate || credential == null) {
					// ask new credential to the user
					string realm = "Proxy"; // ToDo: extract from the proxyAuthenticateValue
					credential = runner.AskCredential(this, realm, needUpdate);
					if (credential == null) {
						credential = new NetworkCredential();
					}
					basicCredential = CreateBasicAuthorizationCredential(credential);

					// Note that this.serverCredentialPersistence may be changed during the runner.AskCredential() call above.
					if (this.isServerCredentialPersistencyProcess) {
						this.serverCredential = credential;
						this.basicServerCredential = basicCredential;
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

			// Server
			settings.SetDnsEndPointValue(SettingNames.Server, this.Server, omitDefault, defaultValue: null);

			//	RetryCount
			settings.SetInt32Value(SettingNames.RetryCount, this.retryCount, omitDefault, defaultValue: DefaultRetryCount);

			return;
		}

		#endregion


		#region privates

		private static byte[] CreateBasicAuthorizationCredential(NetworkCredential credential) {
			// argument checks
			if (credential == null) {
				return null;
			}
			string userName = credential.UserName;
			if (string.IsNullOrEmpty(userName)) {
				return null;
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
