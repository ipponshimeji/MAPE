using System;
using System.Diagnostics;


namespace MAPE.Testing {
	public class SharedInstanceProvider<T> where T: ObjectWithUseCount, new()  {
		#region types

		public class Fixture: IDisposable {
			#region properties

			public T SharedInstance {
				get {
					return SharedInstanceProvider<T>.SharedInstance;
				}
			}

			#endregion


			#region creation and disposal

			public Fixture() {
				SharedInstance.Use();
			}

			public virtual void Dispose() {
				SharedInstance.Unuse();
			}

			#endregion
		}

		#endregion


		#region data

		public static readonly T SharedInstance = new T();

		#endregion
	}
}
