using System;
using System.Collections.Generic;
using System.Net;


namespace MAPE.Server {
    public interface IActualProxy: IDisposable {
		string Description { get; }

		IReadOnlyCollection<DnsEndPoint> GetProxyEndPoints(DnsEndPoint targetEndPoint);

		IReadOnlyCollection<DnsEndPoint> GetProxyEndPoints(Uri targetUri);
	}
}
