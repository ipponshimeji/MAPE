using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;


namespace MAPE.Utils {
	public static class DisposableUtil {
		#region constants

		public const string DefaultErrorLogTemplate = "Fail to dispose the object at '{1}': {0}";

		#endregion


		#region methods

		public static void DisposeSuppressingErrors(this IDisposable target, string errorLogTemplate = null) {
			// argument checks
			if (target == null) {
				return;
			}

			// dispose the target
			try {
				target.Dispose();
			} catch (Exception exception) {
				// log the error
				try {
					string methodName = GetCallerMethodName();
					if (errorLogTemplate == null) {
						errorLogTemplate = DefaultErrorLogTemplate;
					}
					string errorLog = string.Format(errorLogTemplate, exception.Message, methodName);

					Logger.LogError(null, errorLog);
				} catch {
					Logger.LogError(null, "Fail to dispose the object: " + exception.Message);
				}
			}

			return;
		}

		private static string GetCallerMethodName() {
			string methodName = "(unknown method)";
			try {
				// trace stack and find the caller outside this class
				Type thisClass = typeof(DisposableUtil);
				StackTrace stackTrace = new StackTrace(2);  // skip this method and direct caller
				for (int i = 0; i < stackTrace.FrameCount; ++i) {
					StackFrame frame = stackTrace.GetFrame(i);
					MethodBase method = frame.GetMethod();
					Type type = method.DeclaringType;
					if (type != thisClass) {
						// the caller who call the method of this class
						methodName = $"{type.Name}.{method.Name}()";
						break;
					}
				}
			} catch {
				// continue
			}

			return methodName;
		}

		public static void ClearDisposableObject<T>(ref T target, string errorLogTemplate = null) where T: class, IDisposable {
			IDisposable temp = target;
			target = null;
			DisposeSuppressingErrors(temp, errorLogTemplate);

			return;
		}

		public static void ClearDisposableObjects<T>(this ICollection<T> collection, string errorLogTemplate = null) where T: IDisposable {
			// argument checks
			if (collection == null) {
				return;
			}

			// dispose all items in the collection
			foreach (T item in collection) {
				DisposeSuppressingErrors(item, errorLogTemplate);
			}

			// clear the collection
			collection.Clear();

			return;
		}

		public static void ClearDisposableObjects<T, C>(ref C collection, string errorLogTemplate = null) where T : IDisposable where C : ICollection<T> {
			ICollection<T> temp = collection;
			collection = default(C);
			ClearDisposableObjects(temp, errorLogTemplate);

			return;
		}

		public static void ClearDisposableObjects<T>(this T[] array, string errorLogTemplate = null) where T : IDisposable {
			// argument checks
			if (array == null) {
				return;
			}

			// dispose all items in the array
			T value;
			for (int i = 0; i < array.Length; ++i) {
				value = array[i];
				array[i] = default(T);
				DisposeSuppressingErrors(value, errorLogTemplate);
			}

			return;
		}

		public static void ClearDisposableObjects<T>(ref T[] array, string errorLogTemplate = null) where T : IDisposable {
			T[] temp = array;
			array = null;
			ClearDisposableObjects(temp, errorLogTemplate);

			return;
		}

		public static void ClearDisposableObjectsParallelly<T>(this ICollection<T> collection, string errorLogTemplate = null) where T : IDisposable {
			// argument checks
			if (collection == null) {
				return;
			}

			// dispose all items in the collection
			Parallel.ForEach<T>(collection, (item) => { DisposeSuppressingErrors(item, errorLogTemplate); });

			// clear the collection
			collection.Clear();

			return;
		}

		public static void ClearDisposableObjectsParallelly<T, C>(ref C collection, string errorLogTemplate = null) where T : IDisposable where C: ICollection<T> {
			ICollection<T> temp = collection;
			collection = default(C);
			ClearDisposableObjectsParallelly(temp, errorLogTemplate);

			return;
		}

		public static void ClearDisposableObjectsParallelly<T>(this T[] array, string errorLogTemplate = null) where T : IDisposable {
			// argument checks
			if (array == null) {
				return;
			}

			// dispose all items in the array
			Parallel.ForEach<T>(array, (item) => { DisposeSuppressingErrors(item, errorLogTemplate); });

			// clear the array
			for (int i = 0; i < array.Length; ++i) {
				array[i] = default(T);
			}

			return;
		}

		public static void ClearDisposableObjectsParallelly<T>(ref T[] array, string errorLogTemplate = null) where T : IDisposable {
			T[] temp = array;
			array = null;
			ClearDisposableObjectsParallelly(temp, errorLogTemplate);

			return;
		}

		#endregion
	}
}
