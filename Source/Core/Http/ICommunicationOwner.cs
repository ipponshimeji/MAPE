using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MAPE.ComponentBase;
using MAPE.Utils;


namespace MAPE.Http {
	public interface ICommunicationOwner {
		IComponentLogger Logger { get; }

		IHttpComponentFactory ComponentFactory { get; }

		bool UsingProxy { get; }

		IEnumerable<MessageBuffer.Modification> OnCommunicate(int repeatCount, Request request, Response response);

		HttpException OnError(Request request, Exception exception);

		void OnTunnelingStarted(CommunicationSubType communicationSubType);

		void OnTunnelingClosing(CommunicationSubType communicationSubType, Exception exception);
	}
}
