using System;
using MAPE.ComponentBase;


namespace MAPE.Http {
	public interface ICommunicationOwner {
		IComponentLogger Logger { get; }

		IHttpComponentFactory ComponentFactory { get; }

		IMessageIO RequestIO { get; }

		IMessageIO ResponseIO { get; }

		bool ConnectingToProxy { get; }

		bool OnCommunicate(int repeatCount, Request request, Response response);

		void OnResponseProcessed(Request request, Response response, bool resending);

		HttpException OnError(Request request, Exception exception);

		void OnTunnelingStarted(CommunicationSubType communicationSubType);

		void OnTunnelingClosing(CommunicationSubType communicationSubType, Exception exception);
	}
}
