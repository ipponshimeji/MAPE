using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MAPE.Test.TestWebServer {
	public class RequestHandler {
		#region data

		private readonly IRequestHandlerOwner owner;

		private readonly HttpListenerContext context;

		private Task task = null;

		#endregion


		#region properties

		protected IRequestHandlerOwner Owner {
			get {
				return this.owner;
			}
		}

		protected HttpListenerContext Context {
			get {
				return this.context;
			}
		}

		#endregion


		#region creation and disposal

		public RequestHandler(IRequestHandlerOwner owner, HttpListenerContext context) {
			// argument checks
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}
			if (context == null) {
				throw new ArgumentNullException(nameof(context));
			}

			// initialize members
			this.owner = owner;
			this.context = context;
			Debug.Assert(this.task == null);

			return;
		}

		#endregion


		#region methods

		public static bool WaitAll(IEnumerable<RequestHandler> handlers, int millisecondsTimeout) {
			// argument checks
			if (handlers == null) {
				throw new ArgumentNullException(nameof(handlers));
			}
			if (millisecondsTimeout < Timeout.Infinite) {
				throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));
			}

			// wait the given tasks
			Task[] tasks = handlers.Select<RequestHandler, Task>(
				handler => handler.task
			).Where(
				task => task != null && !task.IsCompleted
			).ToArray();
			return (tasks.Length <= 0) ? true : Task.WaitAll(tasks, millisecondsTimeout);
		}

		// Note that this method is not thread-safe.
		public void StartHandling() {
			// state checks
			if (this.task != null) {
				throw new InvalidOperationException("Already handling.");
			}

			// start handling
			try {
				this.task = Task.Run(() => {
					try {
						HandleRequest();
					} catch (Exception exception) {
						OnError(exception);
					}
					OnRequestHandled();
				});
			} catch (Exception exception) {
				OnError(exception);
			}

			return;
		}

		private void OnError(Exception exception) {
			try {
				HandleError(exception);
			} catch {
				// continue
			}
		}

		private void OnRequestHandled() {
			try {
				Debug.Assert(this.owner != null);
				this.owner.OnRequestHandled(this);
			} catch {
				// continue
			}
		}

		#endregion


		#region overridables

		protected virtual void HandleRequest() {
			HttpListenerRequest request = this.context.Request;
			HttpListenerResponse response = this.context.Response;


		}

		protected virtual void HandleError(Exception exception) {
			HttpListenerRequest request = this.context.Request;
			HttpListenerResponse response = this.context.Response;


		}

		#endregion
	}
}
