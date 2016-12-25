using System;
using System.Diagnostics;


namespace MAPE.Http {
	public enum CommunicationSubType {
		Session,		// whole communication session
		UpStream,		// upstream communication
		DownStream,		// downstream communication
	}
}
