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


namespace MAPE.Core {
    public class Connection: TaskingComponent {
		#region constants

		public const string ObjectBaseName = "Connection";

		#endregion


		#region data

		private ConnectionCollection owner = null;

		#endregion


		#region data - synchronized by locking this

		private TcpClient client = null;

		private TcpClient server = null;

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

			// update its state
			lock (this) {
				// state checks
				if (this.owner == null) {
					throw new ObjectDisposedException(this.GetType().Name);
				}

				Task communicatingTask = this.Task;
				if (communicatingTask != null) {
					throw new InvalidOperationException("It already started communication.");
				}
				communicatingTask = new Task(Communicate);
				communicatingTask.ContinueWith(
					(t) => {
						TraceInformation("Completed.");
						this.ObjectName = ObjectBaseName;
						this.owner.OnConnectionCompleted(this);
					}
				);
				this.Task = communicatingTask;

				this.ObjectName = $"{ObjectBaseName}({client.Client.RemoteEndPoint.ToString()})";
				Debug.Assert(this.client == null);
				this.client = client;

				// start communicating task
				communicatingTask.Start();
			}

			return;
		}

		public bool StopCommunication(int millisecondsTimeout = 0) {
			Task communicatingTask;
			lock (this) {
				// state checks
				if (this.owner == null) {
					throw new ObjectDisposedException(this.GetType().Name);
				}

				communicatingTask = this.Task;
				if (communicatingTask == null) {
					// already stopped
					return true;
				}

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
			bool stopConfirmed = false;
			if (millisecondsTimeout != 0) {
				stopConfirmed = communicatingTask.Wait(millisecondsTimeout);
			}

			return stopConfirmed;
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
					server = null;
					openServerError = exception;
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
			} finally {
				lock (this) {
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

			ComponentFactory componentFactory = this.ComponentFactory;
			Request request = componentFactory.AllocRequest(clientStream, serverStream);
			try {
				Response response = componentFactory.AllocResponse(serverStream, clientStream);
				try {
					request.Read();
					request.Write();
					response.Read();
					response.Write();
				} finally {
					componentFactory.ReleaseResponse(response);
				}
			} finally {
				componentFactory.ReleaseRequest(request);
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
