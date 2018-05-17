using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MAPE.Test.TestWebServer {
    public class Server: IRequestHandlerOwner {
		#region data

		private readonly object instanceLocker = new object();

		private readonly List<RequestHandler> requestHandlers = new List<RequestHandler>();

		private Task listeningTask = null;

		private HttpListener listener = null;

		protected string ProxyPrefix { get; }

		protected string DirectPrefix { get; }

		#endregion


		#region properties

		protected bool IsListening {
			get {
				return this.listeningTask != null;
			}
		}

		#endregion


		#region creation and disposal

		public Server(string proxyPrefix, string directPrefix) {
			// argument checks
			if (string.IsNullOrEmpty(proxyPrefix)) {
				throw new ArgumentNullException(nameof(proxyPrefix));
			}
			// directPrefix can be null

			// initialize members
			this.ProxyPrefix = proxyPrefix;
			this.DirectPrefix = directPrefix;

			return;
		}

		#endregion


		#region methods

		public void Start() {
			// start the listeningTask
			lock (this.instanceLocker) {
				// state checks
				if (this.listeningTask != null) {
					throw new InvalidOperationException("Already started.");
				}
				Debug.Assert(this.listener == null);

				HttpListener listener = new HttpListener();
				try {
					// setup HttpListener
					listener.Prefixes.Add(this.ProxyPrefix);
					if (this.DirectPrefix != null) {
						listener.Prefixes.Add(this.DirectPrefix);
					}

					// update status
					this.listener = listener;
					this.listeningTask = Task.Run((Action)Listen);
				} catch {
					if (this.listener != null) {
						this.listener = null;
					}
					throw;
				}
			}

			return;
		}

		private void Listen() {
			// state checks
			HttpListener listener = this.listener;
			if (listener == null) {
				return;
			}

			// start listening
			listener.Start();
			try {
				// Note that Stop() method will clear this.listener on an other thread.
				while (this.listener != null) {
					HttpListenerContext context = listener.GetContext();
					RequestHandler handler = CreateRequestHandler(context);
					try {
						AddRequestHandler(handler);
						handler.StartHandling();
					} catch {
						RemoveRequestHandler(handler);
						// continue
					}
				}
			} catch (ObjectDisposedException) {
				// the listener stopped listening
				// just quit
			} catch (Exception exception) {
				Console.Error.WriteLine(exception.Message);
			} finally {
				lock (this.instanceLocker) {
					this.listener = null;
				}
				listener.Close();
			}

			return;
		}

		public void Stop(int millisecondsTimeout) {
			// argument checks
			if (millisecondsTimeout < Timeout.Infinite) {
				throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));
			}

			// request the listeningTask to stop 
			Task listeningTask;
			RequestHandler[] handlers;
			lock (this.instanceLocker) {
				// state checks
				listeningTask = this.listeningTask;
				if (listeningTask == null) {
					// not listening
					return;
				}

				// stop the listener
				HttpListener listener = this.listener;
				if (listener != null) {
					listener.Abort();
					this.listener = null;
				}

				// clear the request handler list
				handlers = this.requestHandlers.ToArray();
				this.requestHandlers.Clear();
			}

			// wait for the request handling
			bool completed = true;
			completed = RequestHandler.WaitAll(handlers, millisecondsTimeout);

			// wait for the listening task, which usually quits immediately
			completed = listeningTask.Wait(millisecondsTimeout) && completed;

			// update state
			lock (this.instanceLocker) {
				this.listeningTask = null;
			}

			return;
		}

		#endregion


		#region IRequestHandlerOwner

		void IRequestHandlerOwner.OnRequestHandled(RequestHandler handler) {
			// argument checks
			if (handler == null) {
				throw new ArgumentNullException(nameof(handler));
			}

			// remove the request handler from the list
			RemoveRequestHandler(handler);
		}

		#endregion


		#region overridables

		protected virtual RequestHandler CreateRequestHandler(HttpListenerContext context) {
			return new RequestHandler(this, context);
		}

		#endregion


		#region privates

		private void AddRequestHandler(RequestHandler handler) {
			// argument checks
			if (handler == null) {
				throw new ArgumentNullException(nameof(handler));
			}

			// add the request handler to the list
			lock (this.instanceLocker) {
				this.requestHandlers.Add(handler);
			}
		}

		private void RemoveRequestHandler(RequestHandler handler) {
			// argument checks
			if (handler == null) {
				throw new ArgumentNullException(nameof(handler));
			}

			// remove the request handler from the list
			lock (this.instanceLocker) {
				this.requestHandlers.Add(handler);
			}
		}

		#endregion
	}
}
