using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MAPE.Utils;


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


		#region data

		public readonly string CacheName;

		#endregion


		#region data - synchronized by locking instanceLocker

		private readonly object instanceLocker = new object();

		private Queue<T> cache = new Queue<T>();

		private int maxCachedInstanceCount = DefaultMaxCachedInstanceCount;


		// statistics
		private uint allocatedCount = 0;

		private uint releasedCount = 0;

		private uint maxActiveCount = 0;

		#endregion


		#region properties

		public int MaxCachedInstanceCount {
			get {
//				lock (this.instanceLocker) {
					return this.maxCachedInstanceCount;
//				}
			}
			set {
				// argument checks
				if (value < 0) {
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				lock (this.instanceLocker) {
					this.maxCachedInstanceCount = value;
				}
			}
		}

		#endregion


		#region creation and disposal

		public InstanceCache(string cacheName) {
			// initialize members
			this.CacheName = cacheName;

			return;
		}

		public virtual void Dispose() {
			Queue<T> temp;
			lock (this.instanceLocker) {
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

		public void LogStatistics(bool recap) {
			// state checks
			if (Logger.ShouldLog(TraceEventType.Verbose) == false) {
				return;
			}

			// get data
			string cacheName;
			uint allocatedCount;
			uint releasedCount;
			uint maxActiveCount;
			lock (this.instanceLocker) {
				cacheName = this.CacheName;
				allocatedCount = this.allocatedCount;
				releasedCount = this.releasedCount;
				maxActiveCount = this.maxActiveCount;
			}

			// format log message
			string message;
			if (recap) {
				message = $"Statistics: MaxActiveCount: {maxActiveCount}, AllocatedCount: {allocatedCount}, ReleasedCount: {releasedCount}";
			} else {
				message = $"Statistics: ActiveCount: {allocatedCount - releasedCount}, MaxActiveCount: {maxActiveCount}";
			}

			// log
			Logger.LogVerbose(cacheName ?? string.Empty, message);
		}

		protected void DiscardInstanceIgnoringException(T instance) {
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

		protected T AllocInstance() {
			T instance = null;
			lock (this.instanceLocker) {
				// state checks
				Queue<T> cache = this.cache;
				if (cache == null) {
					throw new ObjectDisposedException(GetType().Name);
				}

				// try to reuse a cached instance
				if (0 < cache.Count) {
					instance = cache.Dequeue();
				}

				// create a new instance if there is no cached one
				if (instance == null) {
					instance = CreateInstance();
				}

				// update statistics
				++this.allocatedCount;
				uint activeCount = this.allocatedCount - this.releasedCount;
				if (this.maxActiveCount < activeCount) {
					this.maxActiveCount = activeCount;
				}
			}

			return instance;
		}

		protected void ReleaseInstance(T instance, bool discardInstance = false) {
			// argument checks
			if (instance == null) {
				throw new ArgumentNullException(nameof(instance));
			}

			try {
				lock (this.instanceLocker) {
					// update statistics
					++this.releasedCount;

					// try to cache the instance
					if (discardInstance == false) {
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
	}
}
