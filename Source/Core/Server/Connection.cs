using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MAPE.ComponentBase;
using MAPE.Http;
using MAPE.Utils;

namespace MAPE.Server {
    public class Connection: TaskingComponent, ICommunicationOwner {
		#region constants

		public const string ObjectBaseName = "Connection";

		#endregion


		#region data

		private ConnectionCollection owner = null;

		#endregion


		#region data synchronized by classLocker

		private static object classLocker = new object();

		private static int nextId = 0;

		#endregion


		#region data - synchronized by locking this

		private int id = 0;

		private int retryCount;

		private TcpClient client = null;

		private TcpClient server = null;

		private byte[] proxyCredential = null;

		#endregion


		#region properties

		public Proxy Proxy {
			get {
				return this.owner.Owner;
			}
		}

		public ComponentFactory ComponentFactory {
			get {
				return this.owner.ComponentFactory;
			}
		}

		#endregion


		#region creation and disposal

		public Connection() {
			// initialize members
			this.ObjectName = ObjectBaseName;

			return;
		}

		public override void Dispose() {
			// stop communicating
			StopCommunication();
		}


		public void ActivateInstance(ConnectionCollection owner) {
			// argument checks
			Debug.Assert(owner != null);

			lock (this) {
				// state checks
				if (this.owner != null) {
					throw new InvalidOperationException("The instance is in use.");
				}

				// initialize members
				this.owner = owner;
				this.retryCount = owner.Owner.RetryCount;
				Debug.Assert(this.client == null);
				Debug.Assert(this.server == null);
				Debug.Assert(this.proxyCredential == null);
				lock (classLocker) {
					this.id = nextId++;
				}
			}

			return;
		}

		public void DeactivateInstance() {
			lock (this) {
				// state checks
				if (this.owner == null) {
					// already deactivated
					return;
				}
				if (this.client != null) {
					throw new InvalidOperationException("The instance is still working.");
				}

				// uninitialize members
				Debug.Assert(this.proxyCredential == null);
				Debug.Assert(this.server == null);
				Debug.Assert(this.client == null);
				this.owner = null;
				this.Task = null;
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
				lock (this) {
					// log
					bool verbose = IsLogged(TraceEventType.Verbose);
					if (verbose) {
						LogVerbose($"Starting for {client.Client.RemoteEndPoint.ToString()} ...");
					}

					// state checks
					if (this.owner == null) {
						throw new ObjectDisposedException(this.ObjectName);
					}

					Task communicatingTask = this.Task;
					if (communicatingTask != null) {
						throw new InvalidOperationException("It already started communication.");
					}
					communicatingTask = new Task(Communicate);
					communicatingTask.ContinueWith(
						(t) => {
							base.LogVerbose("Stopped.");
							this.ObjectName = ObjectBaseName;
							this.owner.OnConnectionCompleted(this);
						}
					);
					this.Task = communicatingTask;

					this.ObjectName = $"{ObjectBaseName} <{this.id}>";
					Debug.Assert(this.client == null);
					this.client = client;

					// start communicating task
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
				lock (this) {
					// state checks
					if (this.owner == null) {
						throw new ObjectDisposedException(this.ObjectName);
					}

					communicatingTask = this.Task;
					if (communicatingTask == null) {
						// already stopped
						return true;
					}
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


		#region overridables

		protected virtual IEnumerable<MessageBuffer.Modification> GetModifications(Request request, Response response) {
			// argument checks
			if (request == null) {
				throw new ArgumentNullException(nameof(request));
			}
			// response may be null

			// ToDo: thread protection
			byte[] overridingProxyCredential;
			if (response == null) {
				// first request
				if (request.ProxyAuthorizationSpan.IsZeroToZero == false) {
					// the client specified Proxy-Authorization
					overridingProxyCredential = null;
				} else {
					overridingProxyCredential = this.proxyCredential;
					if (overridingProxyCredential == null) {
						overridingProxyCredential = this.Proxy.GetProxyCredential(null, false);
					}
				}
			} else {
				// re-sending request
				if (response.StatusCode == 407) {
					// the current credential seems to be invalid
					overridingProxyCredential = this.Proxy.GetProxyCredential(response.ProxyAuthenticateValue, true);
				} else {
					// no need to resending
					overridingProxyCredential = null;
				}
			}

			MessageBuffer.Modification[] modifications;
			if (overridingProxyCredential == null) {
				modifications = null;
			} else {
				modifications = new MessageBuffer.Modification[] {
					new MessageBuffer.Modification(
						request.ProxyAuthorizationSpan.IsZeroToZero? request.EndOfHeaderFields: request.ProxyAuthorizationSpan,
						(mb) => { mb.Write(overridingProxyCredential); return true; }	
					)
				};
			}

			return modifications;
		}

		#endregion


		#region ICommunicationOwner - for Communication class only

		ComponentFactory ICommunicationOwner.ComponentFactory {
			get {
				return this.ComponentFactory;
			}
		}

		ILogger ICommunicationOwner.Logger {
			get {
				return this;
			}
		}

		IEnumerable<MessageBuffer.Modification> ICommunicationOwner.GetModifications(int repeatCount, Request request, Response response) {
			// argument checks
			if (request == null) {
				throw new ArgumentNullException(nameof(request));
			}
			if (response == null && repeatCount != 0) {
				throw new ArgumentNullException(nameof(response));
			}

			// retry checks
			IEnumerable<MessageBuffer.Modification> modifications = null;
			if (repeatCount <= this.retryCount) {
				// get actual modifications
				modifications = GetModifications(request, response);
			} else {
				LogWarning("Overruns the retry count. Responding the current response.");
			}

			// log the round trip result
			if (response != null) {
				LogRoundTripResult(request, response, modifications != null);
			}

			return modifications;
		}

		HttpException ICommunicationOwner.OnError(Request request, Exception exception) {
			// argument checks
			// request can be null
			// exception can be null

			HttpException httpException = null;
			try {
				// interpret the exception to HttpException
				// Null httpError means no need to send any error message to the client.
				if (exception != null && request != null && request.MessageRead) {
					httpException = exception as HttpException;
					if (httpException == null) {
						httpException = new HttpException(exception);
						Debug.Assert(httpException.HttpStatusCode == HttpStatusCode.InternalServerError);
					}
				}

				// log the state
				if (exception != null) {
					// report the original exception message (not httpException's)
					LogError($"Error: {exception.Message}");
				}
				if (httpException != null) {
					string method = request?.Method;
					if (string.IsNullOrEmpty(method)) {
						method = "(undetected method)";
					}
					LogError($"Trying to respond an error response: {method} - {httpException.StatusCode}");
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
						StopCommunication();
						LogError($"Error in {direction} tunneling: {exception.Message}");
					} else {
						// shutdown the communication
						bool downStream = (communicationSubType == CommunicationSubType.DownStream);
						lock (this) {
							if (this.server != null) {
								Socket socket = this.server.Client;
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


		#region privates

		/// <summary>
		/// 
		/// </summary>
		/// <remarks>
		/// Note that this method is not thread safe.
		/// You must call this method inside a lock(this) scope.
		/// </remarks>
		private void CloseTcpConnections() {
			TcpClient client;
			TcpClient server;

			// close server connection
			server = this.server;
			this.server = null;
			client = this.client;
			this.client = null;

			if (server != null) {
				try {
					server.Close();
				} catch (Exception exception) {
					LogVerbose($"Exception on closing server connection: {exception.Message}");
					// continue
				}
			}
			if (client != null) {
				try {
					client.Close();
				} catch (Exception exception) {
					LogVerbose($"Exception on closing client connection: {exception.Message}");
					// continue
				}
			}

			return;
		}

		private void Communicate() {
			// preparations
			ConnectionCollection owner;
			Proxy proxy;
			TcpClient client;
			TcpClient server;
			Exception openServerError;
			lock (this) {
				owner = this.owner;
				proxy = this.Proxy;
				client = this.client;
				try {
					server = proxy.OpenServerConnection(client);
					openServerError = null;
				} catch (Exception exception) {
					LogWarning($"Fail to connect the server: {exception.Message}");
					LogWarning($"Sending an error response to the client.");
					server = null;
					openServerError = exception;
					// continue
				}
				this.server = server;
			}

			// communicate
			try {
				using (NetworkStream clientStream = this.client.GetStream()) {
					if (openServerError != null) {
						// the case that server connection is not available
						Response.RespondSimpleError(clientStream, 500, "Not Connected to Actual Proxy");
						LogError($"Cannot connect to the actual proxy '{this.Proxy.Server.Host}:{this.Proxy.Server.Port}'.");
					} else {
						using (NetworkStream serverStream = server.GetStream()) {
							Communication.Communicate(this, clientStream, serverStream);
						}
					}
				}
			} catch (Exception exception) {
				LogError($"Error: {exception.Message}");
				// continue
			} finally {
				LogVerbose("Communication completed.");
				lock (this) {
					this.proxyCredential = null;
					CloseTcpConnections();
				}
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

				if (statusCode < 400) {
					LogInformation(message);
				} else if (statusCode == 407) {
					LogWarning(message);
				} else {
					LogError(message);
				}
			} catch {
				// continue
				// this method should not throw any exception
			}

			return;
		}

		#endregion
	}
}
