using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;


namespace MAPE.Utils {
	public class ScanningAdapter<T> {
		#region data

		private IEnumerator<T> enumerator;

		public bool EndOfData {
			get;
			private set;
		}

		#endregion


		#region properties

		public bool HasMoreData {
			get {
				return !this.EndOfData;
			}
		}

		public T Current {
			get {
				return this.enumerator.Current;
			}
		}

		#endregion


		#region creation and disposal

		public ScanningAdapter(IEnumerator<T> enumerator) {
			// argument checks
			if (enumerator == null) {
				throw new ArgumentNullException(nameof(enumerator));
			}

			// initialize members
			this.enumerator = enumerator;
			this.EndOfData = false;

			return;
		}

		#endregion


		#region methods

		public static Exception CreateEndOfDataException() {
			return new EndOfStreamException();
		}


		public T GetNext() {
			if (this.enumerator.MoveNext() == false) {
				this.EndOfData = true;
				throw CreateEndOfDataException();
			}

			return this.enumerator.Current;
		}

		public bool MoveNext(bool shouldNotEnd = false) {
			if (this.enumerator.MoveNext()) {
				return true;
			} else {
				this.EndOfData = true;
				if (shouldNotEnd) {
					throw CreateEndOfDataException();
				}
				return false;
			}
		}


		public bool Skip(Func<T, bool> isStopPoint, bool shouldNotEnd = false) {
			// argument checks
			if (isStopPoint == null) {
				throw new ArgumentNullException(nameof(isStopPoint));
			}

			// skip data
			T t = this.Current;
			while (isStopPoint(t) == false) {
				if (MoveNext() == false) {
					if (shouldNotEnd) {
						throw new EndOfStreamException();
					} else {
						return false;
					}
				}
				t = this.Current;
			}

			return true;
		}

		public bool Handle(Action<T> handler, Func<T, bool> isStopPoint, bool shouldNotEnd = false) {
			// argument checks
			if (isStopPoint == null) {
				throw new ArgumentNullException(nameof(isStopPoint));
			}
			if (handler == null) {
				return Skip(isStopPoint, shouldNotEnd);
			}

			// handle data
			T t = this.Current;
			while (isStopPoint(t) == false) {
				handler(t);
				if (MoveNext() == false) {
					if (shouldNotEnd) {
						throw new EndOfStreamException();
					} else {
						return false;
					}
				}
				t = this.Current;
			}

			return true;
		}

		#endregion
	}
}
