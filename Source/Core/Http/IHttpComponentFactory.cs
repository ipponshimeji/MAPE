using System;


namespace MAPE.Http {
	public interface IHttpComponentFactory {
		Request AllocRequest(IMessageIO io);

		void ReleaseRequest(Request instance, bool discardInstance = false);

		Response AllocResponse(IMessageIO io);

		void ReleaseResponse(Response instance, bool discardInstance = false);
	}
}
