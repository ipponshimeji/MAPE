using System;
using System.Net;
using MAPE.Command;


namespace MAPE.Server {
    public interface IProxyRunner {
		CredentialInfo GetCredential(string endPoint, string realm, bool needUpdate);
	}
}
