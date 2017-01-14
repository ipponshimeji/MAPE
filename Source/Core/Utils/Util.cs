using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;


namespace MAPE.Utils {
	public static class Util {
		#region constants

		public const char ParameterNameSeparator = '=';

		#endregion


		#region methods - general

		public static void DisposeWithoutFail(IDisposable target) {
			if (target != null) {
				try {
					target.Dispose();
				} catch (Exception exception) {
					string objectName;
					try {
						objectName = target.GetType().FullName;
					} catch {
						objectName = "(unknown)";
					}
					// ToDo: display the name of the caller method
					Logger.LogVerbose("Util.DisposeWithoutFail()", $"Exception at Dispose() on '{objectName}': {exception.Message}");
				}
			}

			return;			
		}

		public static void DisposeWithoutFail<T>(ref T target) where T: class, IDisposable {
			IDisposable temp = target;
			target = null;
			DisposeWithoutFail(temp);
		}


		public static void ClearDisposableObjectList<T>(List<T> list) where T: IDisposable {
			if (list != null) {
				list.ForEach(
					(item) => {
						try {
							if (item != null) {
								item.Dispose();
							}
						} catch {
							// continue
						}
					}
				);
				list.Clear();
			}
		}

		#endregion


		#region methods - configuration
		#endregion


		#region methods
		#endregion


		#region privates
		#endregion
	}
}
