using System;
using System.IO;


namespace MAPE.Http {
	public interface IMessageIO {
		Stream Input { get; }

		Stream Output { get; }

		event EventHandler InputReconnected;
	}
}
