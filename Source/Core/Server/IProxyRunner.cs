using System;
using System.Net;


namespace MAPE.Server {
    public interface IProxyRunner {
		ValueTuple<NetworkCredential, bool> GetCredential(string endPoint, string realm, bool needUpdate);
	}
}
