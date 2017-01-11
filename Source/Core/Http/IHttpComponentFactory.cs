using System;
using System.IO;


namespace MAPE.Http {
	public interface IHttpComponentFactory {
		Request AllocRequest(Stream input, Stream output);

		void ReleaseRequest(Request instance, bool discardInstance = false);

		Response AllocResponse(Stream input, Stream output);

		void ReleaseResponse(Response instance, bool discardInstance = false);
	}
}
