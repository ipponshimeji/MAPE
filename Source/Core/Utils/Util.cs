using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;


namespace MAPE.Utils {
	public static class Util {
		#region methods - disposing

		public static void DisposeWithoutFail(IDisposable target, string errorLogTemplate = null) {
			// argument checks
			if (target == null) {
				return;
			}

			// dispose the target
			try {
				target.Dispose();
			} catch (Exception exception) {
				// log the error
				string methodName = GetCallerMethodName();
				if (errorLogTemplate == null) {
					errorLogTemplate = "Fail to dispose object: at {1}, {0}";
				}
				string errorLog = string.Format(errorLogTemplate, exception.Message, methodName);

				Logger.LogError(null, errorLog);
			}

			return;			
		}

		public static void DisposeWithoutFail<T>(ref T target, string errorLogTemplate = null) where T: class, IDisposable {
			IDisposable temp = target;
			target = null;
			DisposeWithoutFail(temp, errorLogTemplate);
		}

		public static void ClearDisposableObjectList<T>(List<T> list, string errorLogTemplate = null) where T: IDisposable {
			// argument checks
			if (list == null) {
				return;
			}

			// dispose all items in the list
			list.ForEach((item) => { DisposeWithoutFail(item, errorLogTemplate); });

			// clear the list
			list.Clear();

			return;
		}

		public static void ClearDisposableObjectListParallelly<T>(List<T> list, string errorLogTemplate = null) where T : IDisposable {
			// argument checks
			if (list == null) {
				return;
			}

			// dispose all items in the list
			Parallel.ForEach<T>(list, (item) => { DisposeWithoutFail(item, errorLogTemplate); });

			// clear the list
			list.Clear();

			return;
		}

		#endregion


		#region methods - misc

		public static string NormalizeNullToEmpty(string value) {
			return value ?? string.Empty;
		}

		#endregion


		#region privates

		private static string GetCallerMethodName() {
			string methodName = "(unknown method)";
			try {
				// trace stack and find the caller outside this class
				Type thisClass = typeof(Util);
				StackTrace stackTrace = new StackTrace(2);  // skip this method and direct caller
				for (int i = 0; i < stackTrace.FrameCount; ++i) {
					StackFrame frame = stackTrace.GetFrame(i);
					MethodBase method = frame.GetMethod();
					Type type = method.DeclaringType;
					if (type != thisClass) {
						// the caller who call the method of this class
						methodName = $"{type.Name}.{method.Name}()";
					}
				}
			} catch {
				// continue
			}

			return methodName;
		}

		#endregion
	}
}
