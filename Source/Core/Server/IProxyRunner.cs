using System;
using System.Diagnostics;
using System.Net;


namespace MAPE.Server {
    public interface IProxyRunner {
		NetworkCredential GetCredential(Proxy proxy, string realm, bool needUpdate);
	}
}
