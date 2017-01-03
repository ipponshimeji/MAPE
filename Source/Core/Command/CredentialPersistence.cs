using System;


namespace MAPE.Command {
	public enum CredentialPersistence {
		Session,        // keep in a session
		Process,        // keep in a process
		Persistent,     // keep in a configuration file
	}
}
