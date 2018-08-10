using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MAPE.Test.TestWeb;

namespace MAPE.Test.TestWebServer {
    public class ServerBase: IRequestHandlerOwner {
		#region data - synchronized by instanceLocker

		private readonly object instanceLocker = new object();

		private readonly List<RequestHandler> requestHandlers = new List<RequestHandler>();

		private Task listeningTask = null;

		private HttpListener listener = null;

		#endregion


		#region event

		public event ErrorEventHandler OnError = null;

		#endregion


		#region properties

		protected bool IsListening {
			get {
				return this.listeningTask != null;
			}
		}

		#endregion


		#region creation and disposal

		public ServerBase() {
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
					// setup the HttpListener
					SetupListener(listener);

					// start listening
					listener.Start();

					// update state
					this.listener = listener;
					this.listeningTask = Listen();
				} catch {
					Debug.Assert(this.listeningTask == null);
					this.listener = null;
					if (listener != null) {
						listener.Close();
					}
					throw;
				}
			}

			return;
		}

		private async Task Listen() {
			// listen requests
			try {
				// Note that Stop() method will clear this.listener on an other thread.
				HttpListener listener;
				while ((listener = this.listener) != null) {
					HttpListenerContext context = await listener.GetContextAsync();
					RequestHandler handler = CreateRequestHandler(context);
					AddRequestHandlerAndStartIt(handler);
				}
			} catch (HttpListenerException exception) {
				if (exception.ErrorCode == 995) {
					// the listener stopped listening (995: ERROR_OPERATION_ABORTED)
					// just quit
				} else {
					ReportError(exception);
				}
			} catch (Exception exception) {
				ReportError(exception);
			}

			return;
		}

		public bool Stop(int millisecondsTimeout) {
			// argument checks
			if (millisecondsTimeout < Timeout.Infinite) {
				throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));
			}

			// request the listeningTask to stop 
			bool completed = true;
			HttpListener listener = null;
			try {
				Task listeningTask;

				lock (this.instanceLocker) {
					// state checks
					listeningTask = this.listeningTask;
					if (listeningTask == null) {
						// not listening
						return true;
					}

					// stop the listener
					listener = this.listener;
					if (listener != null) {
						this.listener = null;
						listener.Stop();
					}
				}

				// wait for the listening task, which usually quits immediately
				completed = listeningTask.Wait(millisecondsTimeout);

				// wait for the request handling
				RequestHandler[] handlers;
				lock (this.instanceLocker) {
					handlers = this.requestHandlers.ToArray();
					this.requestHandlers.Clear();
				}
				completed = RequestHandler.WaitAll(handlers, millisecondsTimeout) && completed;
			} finally {
				// update state
				lock (this.instanceLocker) {
					this.listeningTask = null;
					Debug.Assert(this.listener == null);
					Debug.Assert(this.requestHandlers.Count == 0);
				}

				// close the listener
				if (listener != null) {
					listener.Close();
				}
			}

			return completed;
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

		protected virtual void ReportError(Exception exception) {
			// argument checks
			Debug.Assert(exception != null);

			ErrorEventHandler onError = this.OnError;
			if (onError != null) {
				onError(this, new ErrorEventArgs(exception));
			}
		}

		protected virtual void SetupListener(HttpListener listener) {
			// argument checks
			Debug.Assert(listener != null);

			listener.AuthenticationSchemeSelectorDelegate = this.SelectAuthenticationScheme;
		}

		protected virtual AuthenticationSchemes SelectAuthenticationScheme(HttpListenerRequest request) {
			return AuthenticationSchemes.Anonymous;
		}

		protected virtual RequestHandler CreateRequestHandler(HttpListenerContext context) {
			return new RequestHandler(this, context);
		}

		#endregion


		#region privates

		private void AddRequestHandlerAndStartIt(RequestHandler handler) {
			// argument checks
			Debug.Assert(handler != null);

			// add the request handler to the list and start it
			lock (this.instanceLocker) {
				this.requestHandlers.Add(handler);
				try {
					handler.StartHandling();
				} catch {
					this.requestHandlers.Remove(handler);
					throw;
				}
			}
		}

		private void RemoveRequestHandler(RequestHandler handler) {
			// argument checks
			Debug.Assert(handler != null);

			// remove the request handler from the list
			lock (this.instanceLocker) {
				this.requestHandlers.Remove(handler);
			}
		}

		#endregion
	}
}
