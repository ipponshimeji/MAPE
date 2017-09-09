using System;
using System.Diagnostics;


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

	public abstract class CacheableInstanceCache<T, TInitParam>: InstanceCache<T> where T : class, ICacheableObject<TInitParam> {
		#region creation and disposal

		public CacheableInstanceCache(string cacheName) : base(cacheName) {
		}

		#endregion


		#region methods

		protected T AllocInstance(TInitParam initParam) {
			// allocate an instance
			T instance = base.AllocInstance();
			try {
				// activate the instance
				instance.OnDecached(initParam);
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
