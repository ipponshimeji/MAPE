using System;
using System.Diagnostics;
using System.IO;
using MAPE.Utils;


namespace MAPE.Http.Test {
	public class DiskMessageSample: MessageSample {
		#region data

		private FileStream sample = null;

		private FileStream output = null;

		#endregion


		#region creation & disposal

		public DiskMessageSample() : base() {
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
			FileStream fileStream = Util.CreateTempFileStream();
			this.sample = fileStream;

			return fileStream;
		}

		protected override Stream CompleteSampleWriting(Stream sampleWriter) {
			// argument checks
			Debug.Assert(sampleWriter == this.sample);

			// reset position
			FileStream sampleReader = this.sample;
			Debug.Assert(sampleReader != null);
			sampleReader.Position = 0;

			return sampleReader;
		}

		protected override Stream CreateOutputWriter() {
			// state checks
			Debug.Assert(this.output == null);

			// create a actual storage
			FileStream fileStream = Util.CreateTempFileStream();
			this.output = fileStream;

			return fileStream;
		}

		protected override Stream CompleteOutputWriting(Stream outputWriter) {
			// argument checks
			// Note that outputWriter may be null.
			Debug.Assert(outputWriter == this.output);

			// reset the stream position
			// Note that outputReader may be null.
			FileStream outputReader = this.output;
			if (outputReader != null) {
				outputReader.Position = 0;
			}

			return outputReader;
		}

		#endregion
	}
}
