using System;
using MAPE.Utils;
using MAPE.Http;
using MAPE.Server.Settings;


namespace MAPE.Server {
    public interface IServerComponentFactory {
		IHttpComponentFactory HttpComponentFactory { get; }

		Proxy CreateProxy(ProxySettings settings);

		Listener CreateListener(Proxy owner, ListenerSettings settings);

		ConnectionCollection CreateConnectionCollection(Proxy owner);

		Connection AllocConnection(ConnectionCollection owner);

		void ReleaseConnection(Connection instance, bool discardInstance = false);
	}
}
