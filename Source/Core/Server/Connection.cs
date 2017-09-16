using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using MAPE.ComponentBase;
using MAPE.Http;
using MAPE.Utils;

namespace MAPE.Server {
    public class Connection: TaskingComponent, ICacheableObject<ConnectionCollection>, ICommunicationOwner {
		#region types

		private struct ServerConnection {
			#region data

			private TcpClient server;

			private string host;

			private int port;

			#endregion


			#region properties

			public bool IsConnecting {
				get {
					return this.server != null;
				}
			}

			public string EndPoint {
				get {
					// state checks
					if (this.server == null) {
						throw CreateNotConnectingException();
					}

					return $"{this.host}:{this.port}";
				}
			}

			public Stream Stream {
				get {
					// state checks
					// Be careful to access this.server only once,
					// otherwise this property must be called in an atomic scope by locking instanceLocker.
					TcpClient server = this.server;
					if (server == null) {
						throw CreateNotConnectingException();
					}

					return server.GetStream();
				}
			}

			public Socket Socket {
				get {
					// state checks
					// Be careful to access this.server only once,
					// otherwise this property must be called in an atomic scope by locking instanceLocker.
					TcpClient server = this.server;
					if (server == null) {
						throw CreateNotConnectingException();
					}

					return server.Client;
				}
			}

			#endregion


			#region methods

			public bool IsEndPoint(string host, int port) {
				return port == this.port && Util.AreSameHostNames(host, this.host);
			}

			public void Connect(string host, int port) {
				// argument checks
				Debug.Assert(string.IsNullOrEmpty(host) == false);
				Debug.Assert(IPEndPoint.MinPort < port && port <= IPEndPoint.MaxPort);

				// state checks
				Debug.Assert(this.server == null);

				// connect to the server
				this.server = new TcpClient(host, port);
				this.host = host;
				this.port = port;

				return;
			}

			public void Disconnect() {
				// disconnect from the server
				this.port = 0;
				this.host = null;

				TcpClient temp = this.server;
				this.server = null;
				if (temp != null) {
					temp.Close();
				}

				return;
			}

			public void Reconnect() {
				// backup the current endpoint information.
				string host = this.host;
				int port = this.port;

				// disconnect from the server
				Disconnect();

				// reconnect to the server
				Connect(host, port);

				return;
			}

			#endregion


			#region privates

			private static InvalidOperationException CreateNotConnectingException() {
				return new InvalidOperationException("It is not connecting to any server.");
			}

			#endregion
		}

		#endregion


		#region constants

		public const string ComponentNameBase = "Connection";

		#endregion


		#region data - regarded as practically read only

		// These fields are set only in OnCaching() and OnDecached() methods.

		private ConnectionCollection owner = null;

		private int retryCount;

		#endregion


		#region data - atomized by locking this.instanceLocker

		private readonly object instanceLocker = new object();

		private TcpClient client = null;

		private ServerConnection server;

		// Stream caches.
		// After the connection is requested to disconnect, the stream objects remain for a while   
		// so that the Communication object can terminate the connection without NullReferenceException. 
		// The streams has been disposed by its owner (TcpClient object) at that time, 
		// so its call causes a ObjectDisposedException, 
		// but it is understandable than NullReferenceException.
		private Stream clientStream = null;
		private Stream serverStream = null;

		private bool connectingToProxy = false;

		private Proxy.BasicCredential proxyCredential = null;

		#endregion


		#region properties

		public Proxy Proxy {
			get {
				return this.owner.Owner;
			}
		}

		public IServerComponentFactory ComponentFactory {
			get {
				return this.owner.ComponentFactory;
			}
		}

		#endregion


		#region creation and disposal

		public Connection(): base(allocateComponentId: false) {
			// initialize members
			this.ComponentName = ComponentNameBase;
			this.server = new ServerConnection();

			return;
		}

		public override void Dispose() {
			// stop communicating
			StopCommunication();
		}

		#endregion


		#region ICacheableObject<ConnectionCollection>

		public void OnCaching() {
			// clear the instance
			lock (this.instanceLocker) {
				// state checks
				if (this.owner == null) {
					// already cleared
					return;
				}
				if (this.client != null) {
					throw new InvalidOperationException("The instance is still in use.");
				}

				// uninitialize members
				Debug.Assert(this.proxyCredential == null);
				Debug.Assert(this.connectingToProxy == false);
				Debug.Assert(this.serverStream == null);
				Debug.Assert(this.clientStream == null);
				Debug.Assert(this.server.IsConnecting == false);
				Debug.Assert(this.client == null);
				this.retryCount = 0;
				this.owner = null;
				this.Task = null;
				this.ComponentName = ComponentNameBase;
			}

			return;
		}

		public void OnDecached(ConnectionCollection owner) {
			// argument checks
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}

			// reset the instance to be reused
			lock (this.instanceLocker) {
				// state checks
				if (this.owner != null) {
					throw new InvalidOperationException("The instance is in use.");
				}

				// initialize members
				this.ParentComponentId = owner.Owner.ComponentId;
				this.ComponentId = Logger.AllocComponentId();
				Debug.Assert(this.Task == null);
				this.owner = owner;
				this.retryCount = owner.Owner.RetryCount;
				Debug.Assert(this.client == null);
				Debug.Assert(this.server.IsConnecting == false);
				Debug.Assert(this.clientStream == null);
				Debug.Assert(this.serverStream == null);
				Debug.Assert(this.connectingToProxy == false);
				Debug.Assert(this.proxyCredential == null);
			}

			return;
		}

		#endregion


		#region methods

		public void StartCommunication(TcpClient client) {
			// argument checks
			if (client == null) {
				throw new ArgumentNullException(nameof(client));
			}

			try {
				// log
				bool verbose = ShouldLog(TraceEventType.Verbose);
				if (verbose) {
					LogVerbose($"Starting for {client.Client.RemoteEndPoint.ToString()} ...");
				}

				lock (this.instanceLocker) {
					// state checks
					if (this.owner == null) {
						throw new ObjectDisposedException(this.ComponentName);
					}
					Task communicatingTask = this.Task;
					if (communicatingTask != null) {
						throw new InvalidOperationException("It already started communication.");
					}

					// prepare a communicating task
					communicatingTask = new Task(Communicate);
					communicatingTask.ContinueWith(
						(t) => {
							LogVerbose("Stopped.");
							this.owner.OnConnectionCompleted(this);
						}
					);
					this.Task = communicatingTask;

					this.ComponentName = $"{ComponentNameBase} <{this.ComponentId}>";
					Debug.Assert(this.client == null);
					this.client = client;
					this.clientStream = client.GetStream();

					// start the communicating task
					communicatingTask.Start();

					// log
					if (verbose) {
						LogVerbose("Started.");
					}
				}
			} catch (Exception exception) {
				LogError($"Fail to start: {exception.Message}");
				throw;
			}

			return;
		}

		public bool StopCommunication(int millisecondsTimeout = 0) {
			bool stopConfirmed = false;
			try {
				Task communicatingTask;
				lock (this.instanceLocker) {
					// state checks
					if (this.owner == null) {
						throw new ObjectDisposedException(this.ComponentName);
					}
					communicatingTask = this.Task;
					if (communicatingTask == null) {
						// already stopped
						return true;
					}

					// log
					LogVerbose("Stopping...");

					// force the connections to close
					// It will cause exceptions on I/O in communicating thread.
					CloseTcpConnections();
				}

				// wait for the completion of the listening task
				// Note that -1 timeout means 'Infinite'.
				if (millisecondsTimeout != 0) {
					stopConfirmed = communicatingTask.Wait(millisecondsTimeout);
				}

				// log
				// "Stopped." will be logged in the continuation of the communicating task. See StartCommunication().
			} catch (Exception exception) {
				LogError($"Fail to stop: {exception.Message}");
				throw;
			}

			return stopConfirmed;
		}

		#endregion


		#region ICommunicationOwner - for Communication class only

		IHttpComponentFactory ICommunicationOwner.ComponentFactory {
			get {
				return this.ComponentFactory.HttpComponentFactory;
			}
		}

		IComponentLogger ICommunicationOwner.Logger {
			get {
				return this;
			}
		}

		Stream ICommunicationOwner.RequestInput {
			get {
				return this.clientStream;
			}
		}

		Stream ICommunicationOwner.RequestOutput {
			get {
				return this.serverStream;
			}
		}

		Stream ICommunicationOwner.ResponseInput {
			get {
				return this.serverStream;
			}
		}

		Stream ICommunicationOwner.ResponseOutput {
			get {
				return this.clientStream;
			}
		}

		bool ICommunicationOwner.ConnectingToProxy {
			get {
				return this.connectingToProxy;
			}
		}

		bool ICommunicationOwner.OnCommunicate(int repeatCount, Request request, Response response) {
			// argument checks
			if (request == null) {
				throw new ArgumentNullException(nameof(request));
			}
			if (request.HostEndPoint == null) {
				throw new HttpException(HttpStatusCode.BadRequest);
			}
			if (response == null && repeatCount != 0) {
				throw new ArgumentNullException(nameof(response));
			}

			// preparations
			bool logVerbose = ShouldLog(TraceEventType.Verbose);
			bool retry = false;
			bool connectingToProxy;
			lock (this.instanceLocker) {
				connectingToProxy = this.connectingToProxy;
			}

			if (response == null) {
				// on before requesting to the server firstly

				// detect the server to be connected
				IActualProxy actualProxy = this.Proxy.ActualProxy;
				IReadOnlyCollection<DnsEndPoint> remoteEndPoints = null;
				if (actualProxy != null) {
					if (request.TargetUri != null) {
						remoteEndPoints = actualProxy.GetProxyEndPoints(request.TargetUri);
					} else {
						remoteEndPoints = actualProxy.GetProxyEndPoints(request.HostEndPoint);
					}
				}
				if (remoteEndPoints != null) {
					connectingToProxy = true;
					LogVerbose($"Connecting to proxy '{actualProxy.Description}'");
				} else {
					DnsEndPoint endPoint = request.HostEndPoint;
					remoteEndPoints = new DnsEndPoint[] {
						endPoint
					};
					connectingToProxy = false;
					LogVerbose($"Connecting directly to '{endPoint.Host}:{endPoint.Port}'");
				}

				// connect to the server
				try {
					EnsureConnectToServer(remoteEndPoints);
					Debug.Assert(this.server.IsConnecting);
					lock (this.instanceLocker) {
						this.connectingToProxy = connectingToProxy;
					}
				} catch (Exception exception) {
					// the case that server connection is not available
					lock (this.instanceLocker) {
						this.connectingToProxy = false;
					}
					LogError($"Cannot connect to the server: {exception.Message}");
					throw new HttpException(HttpStatusCode.BadGateway, "Cannot connect to the server.");
				}
				LogVerbose($"Connected to '{this.server.EndPoint}'");
			}

			// retry check and get modifications
			if (repeatCount <= this.retryCount) {
				// set modifications on the request
				retry = SetModifications(request, response);
				if (retry == false && connectingToProxy == false && request.IsConnectMethod) {
					// ToDo: can be more smart?
					LogDirectTunnelingResult(request);
				}
			} else {
				LogWarning("Overruns the retry count. Responding the current response.");
				Debug.Assert(retry == false);
			}

			if (response != null) {
				// on after responded from the server

				// log the round trip result
				LogRoundTripResult(request, response, retry);

				// manage the connection for non-Keep-Alive mode
				if (response.KeepAliveEnabled == false && !(request.IsConnectMethod && response.StatusCode == 200)) {
					if (retry == false) {
						// disconnect from the server
						DisconnectFromServer();
					} else {
						// reconnect to the server to resend the request
						try {
							ReconnectToServer();
						} catch (Exception exception) {
							LogError($"Cannot connect to the server: {exception.Message}");
							throw new HttpException(HttpStatusCode.BadGateway, "Cannot connect to the server.");
						}
					}
				}
			}

			return retry;
		}

		HttpException ICommunicationOwner.OnError(Request request, Exception exception) {
			// argument checks
			// request can be null
			// exception can be null

			HttpException httpException = null;
			try {
				// interpret the exception to HttpException
				// Null httpError means no need to send any error message to the client.
				if (exception is EndOfStreamException) {
					// an EndOfStreamException means disconnection at an appropriate timing.
					LogVerbose($"The communication ends normally.");
				} else {
					// adjust the exception
					string detail = null;
					if (exception != null && request != null && request.MessageRead) {
						httpException = exception as HttpException;
						if (httpException == null) {
							// wrap the exception into an HttpException
							httpException = new HttpException(exception);
							Debug.Assert(httpException.HttpStatusCode == HttpStatusCode.BadGateway);
							detail = exception.Message;
						} else {
							detail = httpException.InnerException?.Message;
						}
					}

					// log the state
					if (detail != null) {
						// report the original exception message (not httpException's)
						LogError($"Error: {detail}");
					}
					if (httpException != null) {
						string method = request?.Method;
						if (string.IsNullOrEmpty(method)) {
							method = "(undetected method)";
						}
						LogError($"Trying to respond an error response: {method} -> {httpException.StatusCode}, {request?.Host}");
					}
				}
			} catch {
				// continue
				// this method should not throw any exception
			}

			return httpException;
		}

		void ICommunicationOwner.OnTunnelingStarted(CommunicationSubType communicationSubType) {
			// log
			switch (communicationSubType) {
				case CommunicationSubType.Session:
					LogVerbose("Started tunneling mode.");
					break;
				case CommunicationSubType.UpStream:
				case CommunicationSubType.DownStream:
					LogVerbose($"Started {communicationSubType.ToString()} tunneling.");
					break;
			}

			return;
		}

		void ICommunicationOwner.OnTunnelingClosing(CommunicationSubType communicationSubType, Exception exception) {
			switch (communicationSubType) {
				case CommunicationSubType.Session:
					// log
					if (exception != null) {
						LogError($"Error: {exception.Message}");
					}
					LogVerbose("Closing tunneling mode.");
					break;
				case CommunicationSubType.UpStream:
				case CommunicationSubType.DownStream:
					string direction = communicationSubType.ToString();
					if (exception != null) {
						// terminate the communication
						StopCommunication();

						// decide error severity
						TraceEventType eventType = TraceEventType.Error;
						if (exception is IOException) {
							SocketException socketException = exception.InnerException as SocketException;
							if (socketException != null) {
								switch (socketException.SocketErrorCode) {
									case SocketError.ConnectionReset:
									case SocketError.Interrupted:
									case SocketError.TimedOut:
										// may be terminated
										eventType = TraceEventType.Warning;
										break;
								}
							}
						}

						// log
						Log(eventType, $"Error in {direction} tunneling: {exception.Message}");
					} else {
						// shutdown the communication
						bool downStream = (communicationSubType == CommunicationSubType.DownStream);
						lock (this.instanceLocker) {
							if (this.server.IsConnecting) {
								Socket socket = this.server.Socket;
								socket.Shutdown(downStream ? SocketShutdown.Receive : SocketShutdown.Send);
							}
							if (this.client != null) {
								Socket socket = this.client.Client;
								socket.Shutdown(downStream ? SocketShutdown.Send : SocketShutdown.Receive);
							}
						}
					}
					LogVerbose($"Closing {communicationSubType.ToString()} tunneling.");
					break;
			}
		}

		#endregion


		#region overridables

		protected virtual bool SetModifications(Request request, Response response) {
			// argument checks
			if (request == null) {
				throw new ArgumentNullException(nameof(request));
			}
			// response may be null

			// Currently only basic authorization for the proxy is handled.

			// ToDo: convert local function in C# 7
			Func<bool, string, Proxy.BasicCredential> getServerBasicCredentials = (firstRequest, realm) => {
				// set the cached proxy credential 
				Proxy.BasicCredential proxyCredential;
				lock (this.instanceLocker) {
					Proxy.BasicCredential currentProxyCredential = this.proxyCredential;
					proxyCredential = firstRequest ? currentProxyCredential : null;
					if (proxyCredential == null) {
						string endPoint = this.server.EndPoint;
						proxyCredential = this.Proxy.GetServerBasicCredentials(endPoint, realm, firstRequest: firstRequest, oldBasicCredentials: currentProxyCredential);
						this.proxyCredential = proxyCredential; // may be null
					}
				}

				return proxyCredential;
			};

			bool retry = false;
			IReadOnlyCollection<byte> overridingProxyAuthorization = null;
			if (response == null) {
				// before requesting firstly
				if (request.ProxyAuthorizationSpan.IsZeroToZero) {
					// set the cached proxy credential 
					Proxy.BasicCredential proxyCredential = getServerBasicCredentials(true, null);
					overridingProxyAuthorization = proxyCredential?.Bytes;
				}
			} else {
				// after responded from the server
				if (response.StatusCode == 407) {
					// 407: Proxy Authentication Required
					// the current credential seems to be invalid (or null)
					Proxy.BasicCredential proxyCredential = getServerBasicCredentials(false, "Proxy"); // ToDo: extract realm from the field
					overridingProxyAuthorization = proxyCredential?.Bytes;
				} else {
					// no need to resending
					overridingProxyAuthorization = null;
				}
			}

			// set modifications if necessary 
			if (overridingProxyAuthorization != null) {
				// set or overwrite the Proxy-Authorization field.
				Span span = request.ProxyAuthorizationSpan;
				if (span.IsZeroToZero) {
					span = request.EndOfHeaderFields;
				}
				request.AddModification(
					span,
					(modifier) => { modifier.Write(overridingProxyAuthorization); return true; }
				);
				retry = true;
			}

			return retry;
		}

		#endregion


		#region privates

		/// <summary>
		/// 
		/// </summary>
		/// <remarks>
		/// Note that this method is not thread safe.
		/// You must call this method inside a lock(this) scope.
		/// </remarks>
		private void CloseTcpConnections() {
			// close server connection
			TcpClient client = this.client;
			this.client = null;

			try {
				this.server.Disconnect();
			} catch (Exception exception) {
				LogVerbose($"Exception on closing server connection: {exception.Message}");
				// continue
			}
			if (client != null) {
				try {
					client.Close();
				} catch (Exception exception) {
					LogVerbose($"Exception on closing client connection: {exception.Message}");
					// continue
				}
			}
			// No need to clear the serverStream and clientStream at this point.
			// See the comment on the fields. 

			return;
		}

		private void Communicate() {
			// communicate
			try {
				Communication.Communicate(this);
			} catch (Exception exception) {
				LogError($"Error: {exception.Message}");
				// continue
			} finally {
				LogVerbose("Communication completed.");
				lock (this.instanceLocker) {
					CloseTcpConnections();
					this.proxyCredential = null;
					this.connectingToProxy = false;
					this.serverStream = null;
					this.clientStream = null;
				}
			}

			return;
		}

		private void EnsureConnectToServer(IReadOnlyCollection<DnsEndPoint> endPoints) {
			// argument checks
			Debug.Assert(endPoints != null);

			lock (this.instanceLocker) {
				// state checks
				if (this.server.IsConnecting) {
					// check the current server connection
					foreach (DnsEndPoint endPoint in endPoints) {
						if (this.server.IsEndPoint(endPoint.Host, endPoint.Port)) {
							// the current connection is reusable
							return;
						}
					}

					// disconnect to re-connect the connection
					this.server.Disconnect();
				}

				// connect to the server
				Debug.Assert(this.server.IsConnecting == false);  // disconnected at this point
				Exception error = null;
				foreach (DnsEndPoint endPoint in endPoints) {
					try {
						this.server.Connect(endPoint.Host, endPoint.Port);
						this.serverStream = this.server.Stream;
						break;
					} catch (Exception exception) {
						error = exception;
					}
				}
				if (error != null) {
					throw error;
				}
				Debug.Assert(this.server.IsConnecting);
			}

			return;
		}

		private void DisconnectFromServer() {
			lock (this.instanceLocker) {
				// disconnect from the server connection
				this.server.Disconnect();
			}

			return;
		}

		private void ReconnectToServer() {
			lock (this.instanceLocker) {
				// reconnect to the server connection
				this.server.Reconnect();
				this.serverStream = this.server.Stream;
			}

			return;
		}

		private void LogRoundTripResult(Request request, Response response, bool retrying) {
			// argument checks
			Debug.Assert(request != null);
			Debug.Assert(response != null);

			// log the result of one round trip 
			try {
				int statusCode = response.StatusCode;
				string heading = retrying ? "Retrying" : "Respond";
				string message = $"{heading}: {request.Method} -> {statusCode}, {request.Host}";

				LogResult(statusCode, message);
			} catch {
				// continue
				// this method should not throw any exception
			}

			return;
		}

		private void LogDirectTunnelingResult(Request request) {
			// argument checks
			Debug.Assert(request != null);

			// log the result of direct tunneling 
			try {
				int statusCode = 200;
				string message = $"Respond: {request.Method} -> {statusCode}, {request.Host}";

				LogResult(statusCode, message);
			} catch {
				// continue
				// this method should not throw any exception
			}

			return;
		}

		private void LogResult(int statusCode, string message) {
			// argument checks
			Debug.Assert(message != null);

			// log
			if (statusCode < 400) {
				LogInformation(message);
			} else if (statusCode == 407) {
				LogWarning(message);
			} else {
				LogError(message);
			}

			return;
		}

		#endregion
	}
}
