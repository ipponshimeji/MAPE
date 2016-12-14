using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;


namespace MAPE.Core {
    public class Session: IDisposable {
		#region data

		private readonly Proxy owner;

		#endregion


		#region properties
		#endregion


		#region creation and disposal

		public Session(Proxy owner, TcpClient client) {
			// initialize members
			this.owner = owner;

			return;
		}

		public virtual void Dispose() {
			// clear the cache
			return;
		}

		#endregion


		#region methods

		public void Start() {
		}

		public void Stop() {
		}

		#endregion
	}
}
