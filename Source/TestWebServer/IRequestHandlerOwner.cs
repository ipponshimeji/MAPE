using System;

namespace MAPE.Test.TestWebServer {
	public interface IRequestHandlerOwner {
		void OnRequestHandled(RequestHandler handler);
	}
}
