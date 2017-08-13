using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;


namespace MAPE.Utils {
	public static class DisposableUtil {
		#region constants

		public const string LogMessageTemplate = "Fail to dispose the object at '{1}': {0}";

		#endregion


		#region methods

		public static void DisposeSuppressingErrors(this IDisposable target) {
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
					LogError(GetCallerMethodName(), exception.Message);
				} catch {
					// continue, do not throw any Exception from this method
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
					if (type != thisClass && type.Name.StartsWith("<>") == false) {
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

		private static void LogError(string location, string errorMessage) {
			// argument checks
			Debug.Assert(location != null);
			// errorMessage can be null

			// format message and log it
			string logMessage = string.Format(LogMessageTemplate, errorMessage, location);
			Logger.LogError(null, logMessage);

			return;
		}

		public static void ClearDisposableObject<T>(ref T target) where T: class, IDisposable {
			IDisposable temp = target;
			target = null;
			DisposeSuppressingErrors(temp);

			return;
		}

		public static void ClearDisposableObjects<T>(this ICollection<T> target) where T: IDisposable {
			// argument checks
			if (target == null) {
				return;
			}

			// dispose all items in the collection
			foreach (T item in target) {
				DisposeSuppressingErrors(item);
			}

			// clear the collection
			target.Clear();

			return;
		}

		public static void ClearDisposableObjects<T, C>(ref C target) where T : IDisposable where C : ICollection<T> {
			ICollection<T> temp = target;
			target = default(C);
			ClearDisposableObjects(temp);

			return;
		}

		public static void ClearDisposableObjects<T>(this T[] target) where T : IDisposable {
			// argument checks
			if (target == null) {
				return;
			}

			// dispose all items in the array
			T value;
			for (int i = 0; i < target.Length; ++i) {
				value = target[i];
				target[i] = default(T);
				DisposeSuppressingErrors(value);
			}

			return;
		}

		public static void ClearDisposableObjects<T>(ref T[] target) where T : IDisposable {
			T[] temp = target;
			target = null;
			ClearDisposableObjects(temp);

			return;
		}

		public static void ClearDisposableObjectsParallelly<T>(this ICollection<T> target) where T : IDisposable {
			// argument checks
			if (target == null) {
				return;
			}

			// dispose all items in the collection
			IList<string> errorMessages = DisposeObjectsParallelly(target);

			// clear the collection
			target.Clear();

			// log errors
			// Note that errors must be logged here to show the caller method name.
			// (the caller method cannot be detected in the worker threads)
			if (errorMessages != null) {
				LogErrors(GetCallerMethodName(), errorMessages);
			}

			return;
		}

		private static IList<string> DisposeObjectsParallelly<T>(ICollection<T> target) where T : IDisposable {
			// argument checks
			Debug.Assert(target != null);

			// dispose all items in the collection
			object locker = new object();
			List<string> errorMessages = null;
			Action<T> dispose = (item) => {
				if (item != null) {
					try {
						item.Dispose();
					} catch (Exception exception) {
						try {
							lock (locker) {
								if (errorMessages == null) {
									errorMessages = new List<string>();
								}
								errorMessages.Add(exception.Message);
							}
						} catch {
							// continue
						}
					}
				}
			};
			Parallel.ForEach<T>(target, dispose);

			return errorMessages;
		}

		private static void LogErrors(string location, IList<string> errorMessages) {
			// argument checks
			Debug.Assert(location != null);
			Debug.Assert(errorMessages != null);

			// log error messages
			foreach (string errorMessage in errorMessages) {
				LogError(location, errorMessage);
			}

			return;
		}

		public static void ClearDisposableObjectsParallelly<T, C>(ref C target) where T : IDisposable where C: ICollection<T> {
			ICollection<T> temp = target;
			target = default(C);
			ClearDisposableObjectsParallelly(temp);

			return;
		}

		public static void ClearDisposableObjectsParallelly<T>(this T[] target) where T : IDisposable {
			// argument checks
			if (target == null) {
				return;
			}

			// dispose all items in the collection
			IList<string> errorMessages = DisposeObjectsParallelly(target);

			// clear the array
			for (int i = 0; i < target.Length; ++i) {
				target[i] = default(T);
			}

			// log errors
			// Note that errors must be logged here to show the caller method name.
			// (the caller method cannot be detected in the worker threads)
			if (errorMessages != null) {
				LogErrors(GetCallerMethodName(), errorMessages);
			}

			return;
		}

		public static void ClearDisposableObjectsParallelly<T>(ref T[] target) where T : IDisposable {
			T[] temp = target;
			target = null;
			ClearDisposableObjectsParallelly(temp);

			return;
		}

		#endregion
	}
}
