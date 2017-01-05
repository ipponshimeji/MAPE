using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MAPE.Utils;


namespace MAPE.Http {
	public interface ICommunicationOwner {
		ILogger Logger { get; }

		ComponentFactory ComponentFactory { get; }

		IEnumerable<MessageBuffer.Modification> GetModifications(int repeatCount, Request request, Response response);

		void ReconnectServer();

		HttpException OnError(Request request, Exception exception);

		void OnTunnelingStarted(CommunicationSubType communicationSubType);

		void OnTunnelingClosing(CommunicationSubType communicationSubType, Exception exception);
	}
}
