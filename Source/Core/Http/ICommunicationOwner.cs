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

		MessageBuffer.Modification[] GetModifications(int repeatCount, Request request, Response response);

		void OnError(Exception exception);
	}
}
