using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;


namespace MAPE.Utils {
	public class Settings: ISavableToObjectData {
		#region creation and disposal

		protected Settings(IObjectData data) {
		}

		protected Settings(): this(null) {
		}

		#endregion


		#region methods

		protected static FormatException CreateMissingIndispensableSettingException(string settingName) {
			return new FormatException($"The indispensable setting '{settingName}' is missing.");
		}

		protected static ArgumentNullException CreateArgumentNullException(string argName, string settingName) {
			throw new ArgumentNullException(argName, $"The '{settingName}' value must not be null.");
		}

		protected static ArgumentNullException CreateArgumentNullOrEmptyException(string argName, string settingName) {
			throw new ArgumentNullException(argName, $"The '{settingName}' value must not be null or empty.");
		}

		#endregion


		#region ISavableToObjectData

		public void SaveToObjectData(IObjectData data, bool omitDefault = false) {
			// argument checks
			if (data == null) {
				throw new ArgumentNullException(nameof(data));
			}

			SaveTo(data, omitDefault);
		}

		#endregion


		#region overridables

		protected virtual void SaveTo(IObjectData data, bool omitDefault) {
			return;
		}

		public virtual string Validate() {
			return null;
		}

		#endregion
	}
}
