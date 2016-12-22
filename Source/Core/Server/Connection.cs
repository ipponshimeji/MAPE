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


namespace MAPE.Server {
    public class Connection: TaskingComponent {
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
					TraceInformation($"Starting for {client.Client.RemoteEndPoint.ToString()} ...");

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
							TraceInformation("Stopped.");
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
					TraceInformation("Started.");
				}
			} catch (Exception exception) {
				TraceError($"Fail to start: {exception.Message}");
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
					TraceInformation("Stopping...");

					// force the connections to close
					// It will cause exceptions on I/O in communicating thread.
					if (this.client != null) {
						this.client.Close();
						this.client = null;
					}
					if (this.server != null) {
						this.server.Close();
						this.client = null;
					}
				}

				// wait for the completion of the listening task
				// Note that -1 timeout means 'Infinite'.
				if (millisecondsTimeout != 0) {
					stopConfirmed = communicatingTask.Wait(millisecondsTimeout);
				}

				// log
				// "Stopped." will be logged in the continuation of the communicating task. See StartCommunication().
			} catch (Exception exception) {
				TraceError($"Fail to stop: {exception.Message}");
				throw;
			}

			return stopConfirmed;
		}

		#endregion


		#region overridables

		protected virtual MessageBuffer.Modification[] GetModification(int repeatCount, Request request, Response response) {
			// argument checks
			if (request == null) {
				throw new ArgumentNullException(nameof(request));
			}
			if (response == null && repeatCount != 0) {
				throw new ArgumentNullException(nameof(response));
			}
			// ToDo: too many repeat check

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
				TraceInformation($"Response: {response.StatusCode}");
				if (response.StatusCode == 407) {
					overridingProxyCredential = this.Proxy.GetProxyCredential(response.ProxyAuthenticateValue, true);
				} else {
					// no need to resending
					overridingProxyCredential = null;
				}
			}

			MessageBuffer.Modification[] m;
			if (overridingProxyCredential == null) {
				m = null;
			} else {
				m = new MessageBuffer.Modification[] {
					new MessageBuffer.Modification(
						request.ProxyAuthorizationSpan.IsZeroToZero? request.EndOfHeaderFields: request.ProxyAuthorizationSpan,
						(mb) => { mb.Write(overridingProxyCredential); return true; }	
					)
				};
			}

			return m;
		}

		#endregion


		#region privates

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
					TraceWarning($"Fail to connect the server: {exception.Message}");
					TraceWarning($"Sending an error response to the client.");
					server = null;
					openServerError = exception;
					// continue
				}
				this.server = server;
			}

			try {
				using (NetworkStream clientStream = this.client.GetStream()) {
					if (openServerError != null) {
						using (NetworkStream serverStream = server.GetStream()) {
							RespondServerConnectionError(clientStream, openServerError);
						}
					} else {
						using (NetworkStream serverStream = server.GetStream()) {
							CommunicateInternal(clientStream, serverStream);
						}
					}
				}
				TraceInformation("Communication completed.");
			} catch (EndOfStreamException) {
				// the end of communication
				// continue
			} catch (Exception exception) {
				TraceError($"Fail to communicate: {exception.Message}");
				throw;
			} finally {
				lock (this) {
					this.proxyCredential = null;
					this.server = null;
					this.client = null;
				}
				if (server != null) {
					try {
						server.Close();
					} catch {
						// continue
					}
				}
				try {
					client.Close();
				} catch {
					// continue
				}
			}

			return;
		}

		private void CommunicateInternal(Stream clientStream, Stream serverStream) {
			// argument checks
			Debug.Assert(clientStream != null);
			Debug.Assert(serverStream != null);

			bool tunnelMode = false;
			ComponentFactory componentFactory = this.ComponentFactory;
			Request request = componentFactory.AllocRequest(clientStream, serverStream);
			try {
				Response response = componentFactory.AllocResponse(serverStream, clientStream);
				try {
					MessageBuffer.Modification[] modifications;
					while (request.Read()) {
						int repeatCount = 0;
						modifications = GetModification(repeatCount, request, null);
						do {
							request.Write(modifications);
							response.Read();
							++repeatCount;
							modifications = GetModification(repeatCount, request, response);
						} while (modifications != null);
						response.Write();
						if (request.Method == "CONNECT" && response.StatusCode == 200) {
							tunnelMode = true;
							break;
						}
					}
				} catch {
					// ToDo: send error response to client
					byte[] bytes = Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n");
					clientStream.Write(bytes, 0, bytes.Length);
				} finally {
					componentFactory.ReleaseResponse(response);
				}
			} finally {
				componentFactory.ReleaseRequest(request);
			}

			if (tunnelMode) {
				Task.Run(() => { Tunnel(componentFactory, serverStream, clientStream); });
				Tunnel(componentFactory, clientStream, serverStream);
			}
		}

		private static void Tunnel(ComponentFactory componentFactory, Stream input, Stream output) {
			// argument checks
			Debug.Assert(componentFactory != null);
			Debug.Assert(input != null);
			Debug.Assert(output != null);

			byte[] buf = ComponentFactory.AllocMemoryBlock();
			try {
				int readCount;
				do {
					readCount = input.Read(buf, 0, buf.Length);
					if (readCount <= 0) {
						// the end of stream
						break;
					}
					output.Write(buf, 0, readCount);
				} while (true);
			} finally {
				ComponentFactory.FreeMemoryBlock(buf);
				input.Close();
				output.Close();
			}
		}

		private void RespondServerConnectionError(Stream clientStream, Exception error) {
			// argument checks
			Debug.Assert(clientStream != null);

			ComponentFactory componentFactory = this.ComponentFactory;
			Request request = componentFactory.AllocRequest(clientStream, null);
			try {
				Response response = componentFactory.AllocResponse(null, clientStream);
				try {
					// ToDo: log, and respond the error to the client
				} finally {
					componentFactory.ReleaseResponse(response);
				}
			} finally {
				componentFactory.ReleaseRequest(request);
			}
		}

		#endregion
	}
}
