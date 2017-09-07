using System;
using System.Diagnostics;
using MAPE.ComponentBase;


namespace MAPE.Http {
	public interface ICommunicationOwner {
		IComponentLogger Logger { get; }

		IHttpComponentFactory ComponentFactory { get; }

		bool UsingProxy { get; }

		bool OnCommunicate(int repeatCount, Request request, Response response);

		HttpException OnError(Request request, Exception exception);

		void OnTunnelingStarted(CommunicationSubType communicationSubType);

		void OnTunnelingClosing(CommunicationSubType communicationSubType, Exception exception);
	}
}
