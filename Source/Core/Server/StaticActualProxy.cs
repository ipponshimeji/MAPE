using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using MAPE.Utils;


namespace MAPE.Server {
    public sealed class StaticActualProxy: IActualProxy {
		#region data

		private DnsEndPoint[] proxyEndPoints;

		#endregion


		#region creation & disposal

		public StaticActualProxy(params DnsEndPoint[] proxyEndPoints) {
			// argument checks
			if (proxyEndPoints == null) {
				throw new ArgumentNullException(nameof(proxyEndPoints));
			}
			if (proxyEndPoints.Length <= 0) {
				throw new ArgumentException("It has no element.", nameof(proxyEndPoints));
			}
			if (proxyEndPoints.Any(ep => ep == null)) {
				throw new ArgumentException("It has null element.", nameof(proxyEndPoints));
			}

			// initialize members
			this.proxyEndPoints = proxyEndPoints;

			return;
		}

		public void Dispose() {
		}

		#endregion


		#region IActualProxy

		public string Description {
			get {
				return string.Join(", ", this.proxyEndPoints.Select((ep) => $"{ep.Host}:{ep.Port}"));
			}
		}

		public IReadOnlyCollection<DnsEndPoint> GetProxyEndPoints(DnsEndPoint targetEndPoint) {
			// argument checks
			if (targetEndPoint == null) {
				throw new ArgumentNullException(nameof(targetEndPoint));
			}

			return this.proxyEndPoints;
		}

		public IReadOnlyCollection<DnsEndPoint> GetProxyEndPoints(Uri targetUri) {
			// argument checks
			if (targetUri == null) {
				throw new ArgumentNullException(nameof(targetUri));
			}

			return this.proxyEndPoints;
		}

		#endregion
	}
}
