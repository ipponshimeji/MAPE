using System;
using MAPE.Utils;
using MAPE.Http;


namespace MAPE.Server {
    public interface IServerComponentFactory {
		IHttpComponentFactory HttpComponentFactory { get; }

		Proxy CreateProxy(Settings settings);

		Listener CreateListener(Proxy owner, Settings settings);

		ConnectionCollection CreateConnectionCollection(Proxy owner);

		Connection AllocConnection(ConnectionCollection owner);

		void ReleaseConnection(Connection instance, bool discardInstance = false);
	}
}
