using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;


namespace MAPE.Http {
	public struct Modifier {
		#region data

		public readonly Stream output;

		#endregion


		#region creation and disposal

		internal Modifier(Stream output) {
			// argument checks
			Debug.Assert(output != null);

			// initialize members
			this.output = output;

			return;
		}

		#endregion


		#region methods

		public void Write(byte[] data, bool appendCRLF = false) {
			// write data
			if (data != null && 0 < data.Length) {
				this.output.Write(data, 0, data.Length);
			}
			if (appendCRLF) {
				this.output.WriteByte(MessageBuffer.CR);
				this.output.WriteByte(MessageBuffer.LF);
			}

			return;
		}

		public void Write(IReadOnlyCollection<byte> data, bool appendCRLF = false) {
			Write(data?.ToArray(), appendCRLF);
		}

		public void WriteASCIIString(string str, bool appendCRLF = false) {
			// write data
			byte[] data;
			if (string.IsNullOrEmpty(str)) {
				data = null;
			} else {
				data = Encoding.ASCII.GetBytes(str);
			}
			Write(data, appendCRLF);

			return;
		}

		#endregion
	}
}
