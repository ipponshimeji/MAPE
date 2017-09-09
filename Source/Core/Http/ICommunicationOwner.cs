using System;
using System.IO;
using MAPE.ComponentBase;


namespace MAPE.Http {
	public interface ICommunicationOwner {
		IComponentLogger Logger { get; }

		IHttpComponentFactory ComponentFactory { get; }

		Stream RequestInput { get; }

		Stream RequestOutput { get; }

		Stream ResponseInput { get; }

		Stream ResponseOutput { get; }

		bool ConnectingToProxy { get; }

		bool OnCommunicate(int repeatCount, Request request, Response response);

		HttpException OnError(Request request, Exception exception);

		void OnTunnelingStarted(CommunicationSubType communicationSubType);

		void OnTunnelingClosing(CommunicationSubType communicationSubType, Exception exception);
	}
}
