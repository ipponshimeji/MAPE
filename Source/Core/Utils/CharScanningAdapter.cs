using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;


namespace MAPE.Utils {
	public class CharScanningAdapter: ScanningAdapter<char> {
		#region data

		private readonly StringBuilder stockBuffer = new StringBuilder();

		#endregion


		#region properties

		public StringBuilder StockBuffer {
			get {
				return this.stockBuffer;
			}
		}

		#endregion


		#region creation and disposal

		public CharScanningAdapter(IEnumerator<char> enumerator, StringBuilder stockBuffer): base(enumerator) {
			// argument checks
			if (stockBuffer == null) {
				stockBuffer = new StringBuilder();
			}

			// initialize members
			this.stockBuffer = stockBuffer;

			return;
		}

		public CharScanningAdapter(IEnumerator<char> enumerator): this(enumerator, null) {
		}

		#endregion


		#region methods

		public bool ReadAndMoveNext(StringBuilder buf, bool shouldNotEnd = false) {
			// argument checks
			if (buf == null) {
				throw new ArgumentNullException(nameof(buf));
			}

			buf.Append(this.Current);
			return MoveNext(shouldNotEnd);
		}

		public bool ReadToStockBufferAndMoveNext(bool shouldNotEnd = false) {
			return ReadAndMoveNext(this.stockBuffer, shouldNotEnd);
		}

		public bool Read(StringBuilder buf, Func<char, bool> isStopPoint, bool shouldNotEnd = false) {
			// argument checks
			if (buf == null) {
				throw new ArgumentNullException(nameof(buf));
			}
			Debug.Assert(isStopPoint != null);

			return Handle((c) => { buf.Append(c); }, isStopPoint, shouldNotEnd);
		}

		public bool ReadToStockBuffer(Func<char, bool> isStopPoint, bool shouldNotEnd = false) {
			return Read(this.stockBuffer, isStopPoint, shouldNotEnd);
		}

		public string ExtractFromSockBuffer(bool preserve = false) {
			string value = this.stockBuffer.ToString();
			if (preserve == false) {
				this.stockBuffer.Clear();
			}

			return value;
		}

		public string Extract(Func<char, bool> isStopPoint, bool shouldNotEnd = false) {
			ReadToStockBuffer(isStopPoint, shouldNotEnd);
			return ExtractFromSockBuffer();
		}

		#endregion
	}
}
