using System;


namespace MAPE.Testing {
	public class ObjectWithUseCount {
		#region data - synchronized by useCountLocker

		private readonly object useCountLocker = new object();

		protected int UseCount { get; private set; } = 0;

		#endregion


		#region creation and disposal

		public ObjectWithUseCount() {
		}

		#endregion


		#region methods

		public void Use() {
			lock (this.useCountLocker) {
				// state checks
				int count = this.UseCount;
				if (count < 0) {
					throw new InvalidOperationException("invalid state");
				}
				if (count == Int32.MaxValue) {
					throw new InvalidOperationException("use count overflow");
				}

				// start the server process
				if (count == 0) {
					// Note that the Start() may throw an exception on error.
					OnUsed();
				}

				// increment the use count
				++this.UseCount;
			}

			return;
		}

		public void Unuse() {
			lock (this.useCountLocker) {
				// state checks
				if (this.UseCount <= 0) {
					throw new InvalidOperationException("invalid state");
				}

				// stop the server if necessary
				if (--this.UseCount == 0) {
					OnUnused();
				}
			}
		}

		#endregion


		#region overridables

		protected virtual void OnUsed() {
		}

		protected virtual void OnUnused() {
		}

		#endregion
	}
}
