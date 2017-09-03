using System;


namespace MAPE.Http {
	public enum MessageReadingState {
		// error state
		Error = -1,

		// none was read
		None = 0,

		// header part was read
		Header,

		// body part was read
		Body,

		// body part was redirected,
		// so the Message object does not store body contents
		BodyRedirected,
	}
}
