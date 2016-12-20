using System;


namespace MAPE.Utils {
	public struct Parameter {
		#region data

		public readonly string Name;

		public readonly string Value;

		#endregion


		#region properties

		public bool IsNullOrEmptyName {
			get {
				return string.IsNullOrEmpty(this.Name);
			}
		}

		public bool IsNullOrEmptyValue {
			get {
				return string.IsNullOrEmpty(this.Value);
			}
		}

		#endregion


		#region creation and disposal

		public Parameter(string name, string value) {
			// initialize members
			this.Name = name;
			this.Value = value;

			return;
		}

		#endregion


		#region methods

		public static bool AreEqualNames(string name1, string name2) {
			// case-insensitive
			return string.Compare(name1, name2, StringComparison.InvariantCultureIgnoreCase) == 0;
		}

		public bool IsName(string name) {
			return AreEqualNames(this.Name, name);
		}

		#endregion
	}
}
