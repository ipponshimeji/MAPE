using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MAPE.Utils;


namespace MAPE.ComponentBase {
	public abstract class CacheableInstanceCache<T>: InstanceCache<T> where T: class, ICacheableObject {
		#region creation and disposal

		public CacheableInstanceCache(string cacheName): base(cacheName) {
		}

		#endregion


		#region methods

		protected new T AllocInstance() {
			// allocate an instance
			T instance = base.AllocInstance();
			try {
				// activate the instance
				instance.OnDecached();
			} catch {
				// do not cache back the instance in error 
				DiscardInstanceIgnoringException(instance);
				throw;
			}

			return instance;
		}

		protected new void ReleaseInstance(T instance, bool discardInstance = false) {
			// argument checks
			if (instance == null) {
				throw new ArgumentNullException(nameof(instance));
			}

			// deactivate the instance and try to cache it
			try {
				instance.OnCaching();
				base.ReleaseInstance(instance, discardInstance);
			} catch {
				// do not cahce back the instance in error 
				DiscardInstanceIgnoringException(instance);
				// continue
			}

			return;
		}

		#endregion
	}
}
