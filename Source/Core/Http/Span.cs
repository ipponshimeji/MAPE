using System;


namespace MAPE.Http {
	public struct Span {
		#region data

		public static readonly Span ZeroToZero = new Span(0, 0);

		public readonly int Start;

		public readonly int End;

		#endregion


		#region properties

		public bool IsZeroToZero {
			get {
				return this.Start == 0 && this.End == 0;
			}
		}

		#endregion


		#region creation and disposal

		public Span(int start, int end) {
			// argument checks
			if (start < 0) {
				throw new ArgumentOutOfRangeException(nameof(start));
			}
			if (end < start) {
				throw new ArgumentOutOfRangeException(nameof(end));
			}

			// initialize members
			this.Start = start;
			this.End = end;

			return;
		}

		public Span(Span src) {
			// initialize members
			this.Start = src.Start;
			this.End = src.End;

			return;
		}

		#endregion


		#region operators

		public static bool operator == (Span x, Span y) {
			return x.Start == y.Start && x.End == y.End;
		}

		public static bool operator !=(Span x, Span y) {
			return !(x == y);
		}

		#endregion


		#region overrides

		public override bool Equals(object obj) {
			if (obj is Span) {
				return (this == (Span)obj);
			} else {
				return false;
			}
		}

		public override int GetHashCode() {
			return this.Start ^ this.End;
		}

		#endregion
	}
}
