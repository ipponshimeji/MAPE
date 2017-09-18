using System;
using System.IO;


namespace MAPE.Http {
	public class MessageIO: IMessageIO {
		#region data

		private Stream input;

		private Stream output;

		#endregion


		#region creation

		public MessageIO(Stream input, Stream output) {
			// argument checks
			// input and output can be null

			// initialize members
			this.input = input;
			this.output = output;
		}

		#endregion


		#region ICommunicationIO

		public Stream Input {
			get {
				return this.input;
			}
		}

		public Stream Output {
			get {
				return this.output;
			}
		}

		public event EventHandler InputReconnected = null;

		#endregion


		#region methods

		public void SetInput(Stream value) {
			// argument checks
			// value can be null

			if (this.input != value) {
				this.input = value;
				OnInputReconnected();
			}
		}

		public void SetOutput(Stream value) {
			// argument checks
			// value can be null

			this.output = value;
		}

		#endregion


		#region privates

		private void OnInputReconnected() {
			this.InputReconnected?.Invoke(this, EventArgs.Empty);
		}

		#endregion
	}
}
