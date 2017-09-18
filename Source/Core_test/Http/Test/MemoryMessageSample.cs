using System;
using System.Diagnostics;
using System.IO;
using MAPE.Utils;


namespace MAPE.Http.Test {
	public class MemoryMessageSample: MessageSample {
		#region data

		private MemoryStream sample = null;

		private MemoryStream output = null;

		#endregion


		#region creation & disposal

		public MemoryMessageSample() : base() {
		}

		public override void Dispose() {
			// dispose resources
			DisposableUtil.ClearDisposableObject(ref this.output);
			DisposableUtil.ClearDisposableObject(ref this.sample);

			return;
		}

		#endregion


		#region overrides

		protected override Stream CreateSampleWriter() {
			// state checks
			Debug.Assert(this.sample == null);

			// create a sample storage
			MemoryStream memoryStream = new MemoryStream();
			this.sample = memoryStream;

			return memoryStream;
		}

		protected override Stream CompleteSampleWriting(Stream sampleWriter) {
			// argument checks
			Debug.Assert(sampleWriter == this.sample);

			// reset position
			MemoryStream sampleReader = this.sample;
			Debug.Assert(sampleReader != null);
			sampleReader.Position = 0;

			return sampleReader;
		}

		protected override Stream CreateOutputWriter() {
			// state checks
			Debug.Assert(this.output == null);

			// create a actual storage
			MemoryStream memoryStream = new MemoryStream();
			this.output = memoryStream;

			return memoryStream;
		}

		protected override Stream CompleteOutputWriting(Stream outputWriter) {
			// argument checks
			// Note that outputWriter may be null.
			Debug.Assert(outputWriter == this.output);

			// reset the stream position
			// Note that outputReader may be null.
			MemoryStream outputReader = this.output;
			if (outputReader != null) {
				outputReader.Position = 0;
			}

			return outputReader;
		}

		#endregion
	}
}
