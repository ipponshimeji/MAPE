using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using MAPE.Utils;


namespace MAPE.Command.Settings {
	public class CredentialSettings: MAPE.Utils.Settings {
		#region types

		public static class SettingNames {
			#region constants

			public const string EndPoint = "EndPoint";

			public const string UserName = "UserName";

			public const string ProtectedPassword = "ProtectedPassword";

			public const string Persistence = "Persistence";

			public const string EnableAssumptionMode = "EnableAssumptionMode";

			#endregion
		}

		public static class Defaults {
			#region constants

			public const string EndPoint = "";

			public const string UserName = "";

			public const string Password = "";

			public const CredentialPersistence Persistence = CredentialPersistence.Persistent;

			public const bool EnableAssumptionMode = true;

			#endregion


			#region methods

			public static bool IsDefaultEndPoint(string value) {
				return string.Compare(value, EndPoint, StringComparison.InvariantCultureIgnoreCase) == 0;
			}

			public static bool IsDefaultUserName(string value) {
				return string.Compare(value, UserName, StringComparison.InvariantCultureIgnoreCase) == 0;
			}

			public static bool IsDefaultPassword(string value) {
				return string.Compare(value, Password, StringComparison.InvariantCultureIgnoreCase) == 0;
			}

			#endregion
		}

		#endregion


		#region data

		private static readonly byte[] entropy = Encoding.UTF8.GetBytes("認証プロキシ爆発しろ");


		private string endPoint;

		private string userName;

		private string password;

		public CredentialPersistence Persistence { get; set; }

		public bool EnableAssumptionMode { get; set; }

		#endregion


		#region properties

		public string EndPoint {
			get {
				return this.endPoint;
			}
			set {
				this.endPoint = Util.NormalizeNullToEmpty(value);
			}
		}

		public string UserName {
			get {
				return this.userName;
			}
			set {
				this.userName = Util.NormalizeNullToEmpty(value);
			}
		}

		public string Password {
			get {
				return this.password;
			}
			set {
				this.password = Util.NormalizeNullToEmpty(value);
			}
		}

		#endregion


		#region creation and disposal

		public CredentialSettings(IObjectData data): base(data) {
			// prepare settings
			string endPoint = Defaults.EndPoint;
			string userName = Defaults.UserName;
			string password = string.Empty;
			CredentialPersistence persistence = Defaults.Persistence;
			bool enableAssumptionMode = Defaults.EnableAssumptionMode;
			if (data != null) {
				// get settings from data
				endPoint = data.GetStringValue(SettingNames.EndPoint, endPoint);
				userName = data.GetStringValue(SettingNames.UserName, userName);
				password = data.GetValue(SettingNames.ProtectedPassword, password, ExtractPasswordValue);
				persistence = (CredentialPersistence)data.GetEnumValue(SettingNames.Persistence, persistence, typeof(CredentialPersistence));
				enableAssumptionMode = data.GetBooleanValue(SettingNames.EnableAssumptionMode, enableAssumptionMode);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.EndPoint = endPoint;
				this.UserName = userName;
				this.Password = password;
				this.Persistence = persistence;
				this.EnableAssumptionMode = enableAssumptionMode;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public CredentialSettings(): this(null) {
		}

		#endregion


		#region overrides

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// save settings
			data.SetStringValue(SettingNames.EndPoint, this.EndPoint, omitDefault, Defaults.IsDefaultEndPoint(this.EndPoint));
			data.SetStringValue(SettingNames.UserName, this.UserName, omitDefault, Defaults.IsDefaultUserName(this.UserName));
			data.SetValue(SettingNames.ProtectedPassword, this.Password, CreatePasswordValue, omitDefault, Defaults.IsDefaultPassword(this.Password));
			data.SetEnumValue(SettingNames.Persistence, this.Persistence, omitDefault, this.Persistence == Defaults.Persistence);
			data.SetBooleanValue(SettingNames.EnableAssumptionMode, this.EnableAssumptionMode, omitDefault, this.EnableAssumptionMode == Defaults.EnableAssumptionMode);

			return;
		}

		#endregion


		#region privates

		public static string ExtractPasswordValue(IObjectDataValue value) {
			// argument checks
			Debug.Assert(value != null);

			// extract password value
			string protectedPassword = value.ExtractStringValue();
			return string.IsNullOrEmpty(protectedPassword) ? string.Empty : UnprotectPassword(protectedPassword);
		}

		public static IObjectDataValue CreatePasswordValue(IObjectData data, string value) {
			// argument checks
			Debug.Assert(data != null);

			// create password value
			return data.CreateValue(string.IsNullOrEmpty(value) ? string.Empty : ProtectPassword(value));
		}

		private static string ProtectPassword(string password) {
			// argument checks
			Debug.Assert(password != null);

			// encrypt the password by ProtectedData API
			// The password is encrypted with the key of the current user.
			// The protected value transfered from other machine or user cannot be decrypted.
			byte[] bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), entropy, DataProtectionScope.CurrentUser);
			return Convert.ToBase64String(bytes);
		}

		private static string UnprotectPassword(string encryptedPassword) {
			// argument checks
			Debug.Assert(encryptedPassword != null);

			// decrypt the password by ProtectedData API.
			byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(encryptedPassword), entropy, DataProtectionScope.CurrentUser);
			return Encoding.UTF8.GetString(bytes);
		}

		#endregion
	}
}
