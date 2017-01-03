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
