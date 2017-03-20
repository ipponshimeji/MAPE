using System;
using MAPE.Command.Settings;


namespace MAPE.Server {
    public interface IProxyRunner {
		CredentialSettings GetCredential(string endPoint, string realm, bool needUpdate);
	}
}
