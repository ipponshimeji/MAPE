using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace MAPE.ComponentBase {
	/// <summary>
	/// The class to cache instances so that instances are be reused easily.
	/// It may prevent 'garbage' increasing and frequent GC if the target
	/// class is, for example, large, newed frequently and its life is short.
	/// </summary>
	/// <remarks>
	/// You must program carefully so that the cached instance, which is in deactivated state,
	/// is not accessed.
	/// </remarks>
	/// <typeparam name="T"></typeparam>
	public abstract class InstanceCache<T>: IDisposable where T: class {
		#region constants

		public const int DefaultMaxCachedInstanceCount = 8;

		#endregion


		#region data - synchronized by locking this

		private Queue<T> cache = new Queue<T>();

		private int maxCachedInstanceCount = DefaultMaxCachedInstanceCount;

		#endregion


		#region properties

		public int MaxCachedInstanceCount {
			get {
//				lock (this) {
					return this.maxCachedInstanceCount;
//				}
			}
			set {
				// argument checks
				if (value < 0) {
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				lock (this) {
					this.maxCachedInstanceCount = value;
				}
			}
		}

		#endregion


		#region creation and disposal

		public InstanceCache() {
		}

		public virtual void Dispose() {
			Queue<T> temp;
			lock (this) {
				// clear the instance cache
				temp = this.cache;
				this.cache = null;
			}

			// discard instances
			if (temp != null) {
				while (0 < temp.Count) {
					DiscardInstanceIgnoringException(temp.Dequeue());
				}
			}

			return;
		}

		#endregion


		#region methods

		protected T AllocInstance() {
			T instance = null;
			lock (this) {
				// state checks
				Queue<T> cache = this.cache;
				if (cache == null) {
					throw new ObjectDisposedException(GetType().Name);
				}

				// try to reuse a cached instance
				if (0 < cache.Count) {
					instance = cache.Dequeue();
				}
			}

			// create a new instance if there is no cached one
			if (instance == null) {
				instance = CreateInstance();
			}

			return instance;
		}

		protected void ReleaseInstance(T instance, bool discardInstance = false) {
			// argument checks
			if (instance == null) {
				throw new ArgumentNullException(nameof(instance));
			}

			// try to cache the instance
			try {
				if (discardInstance == false) {
					lock (this) {
						Queue<T> cache = this.cache;
						if (cache != null && cache.Count < this.maxCachedInstanceCount) {
							cache.Enqueue(instance);
							instance = null;
						}
					}
				}
			} catch {
				// continue
			}

			// discard the instance if not cached
			if (instance != null) {
				DiscardInstanceIgnoringException(instance);
			}

			return;
		}

		#endregion


		#region overridables

		protected abstract T CreateInstance();

		protected virtual void DiscardInstance(T instance) {
			// do nothing by default
		}

		#endregion


		#region privates

		private void DiscardInstanceIgnoringException(T instance) {
			// argument checks
			Debug.Assert(instance != null);

			// discard the instance ignoring exception
			try {
				DiscardInstance(instance);
			} catch {
				// continue
			}

			return;
		}

		#endregion
	}
}
