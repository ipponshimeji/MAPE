using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace MAPE.Utils {
	public class Settings: ISavableToObjectData {
		#region data

		public const IObjectData NullObjectData = null;

		#endregion


		#region creation and disposal

		protected Settings(IObjectData data) {
		}

		protected Settings(): this(NullObjectData) {
		}

		// cloning constructor
		protected Settings(Settings src) {
		}

		#endregion


		#region methods

		public static T Clone<T>(T src) where T : Settings {
			return (src == null)? null: (T)src.Clone();
		}

		public static T[] Clone<T>(IEnumerable<T> src) where T : Settings {
			return (src == null)? null: src.Select(t => Clone(t)).ToArray();
		}

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

		protected virtual Settings Clone() {
			return new Settings(this);
		}

		protected virtual void SaveTo(IObjectData data, bool omitDefault) {
			return;
		}

		public virtual string Validate() {
			return null;
		}

		#endregion
	}
}
