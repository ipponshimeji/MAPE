using System;


namespace MAPE.Http {
	public interface IHttpComponentFactory {
		Request AllocRequest();

		void ReleaseRequest(Request instance, bool discardInstance = false);

		Response AllocResponse();

		void ReleaseResponse(Response instance, bool discardInstance = false);
	}
}
