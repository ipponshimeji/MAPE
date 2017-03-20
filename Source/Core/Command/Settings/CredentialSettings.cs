using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
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
				return AreSameEndPoints(value, EndPoint);
			}

			public static bool IsDefaultUserName(string value) {
				return AreSameUserNames(value, UserName);
			}

			public static bool IsDefaultPassword(string value) {
				return AreSamePasswords(value, Password);
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

		public CredentialSettings(): this(NullObjectData) {
		}

		public CredentialSettings(CredentialSettings src): base(src) {
			// argument checks
			if (src == null) {
				throw new ArgumentNullException(nameof(src));
			}

			// clone members
			this.EndPoint = src.EndPoint;
			this.UserName = src.UserName;
			this.Password = src.Password;
			this.Persistence = src.Persistence;
			this.EnableAssumptionMode = src.EnableAssumptionMode;

			return;
		}

		public CredentialSettings(string endPoint, string userName, string password, CredentialPersistence persistence, bool enableAssumptionMode): base() {
			// argument checks
			// endPoint can be null (will be normalized to empty string)
			// userName can be null (will be normalized to empty string)
			// password can be null (will be normalized to empty string)

			// initialize members
			this.EndPoint = endPoint;
			this.UserName = userName;
			this.Password = password;
			this.Persistence = persistence;
			this.EnableAssumptionMode = enableAssumptionMode;

			return;
		}

		#endregion


		#region methods

		public static bool AreSameEndPoints(string endPoint1, string endPoint2) {
			// case-insensitive
			return string.Compare(endPoint1, endPoint2, StringComparison.InvariantCultureIgnoreCase) == 0;
		}

		public static bool AreSameUserNames(string userName1, string userName2) {
			// case-sensitive
			return string.Compare(userName1, userName2, StringComparison.Ordinal) == 0;
		}

		public static bool AreSamePasswords(string password1, string password2) {
			// case-sensitive
			return string.Compare(password1, password2, StringComparison.Ordinal) == 0;
		}

		public NetworkCredential GetNetworkCredential() {
			// Note that the endPoint is stored as 'Domain' property of the NetworkCredential object.
			return new NetworkCredential(this.UserName, this.Password, this.EndPoint);
		}

		#endregion


		#region overrides

		public override bool Equals(object obj) {
			// argument checks
			CredentialSettings that = obj as CredentialSettings;
			if (that == null) {
				return false;
			}

			return (
				this.EnableAssumptionMode == that.EnableAssumptionMode &&
				this.Persistence == that.Persistence &&
				AreSameEndPoints(this.EndPoint, that.EndPoint) &&
				AreSameUserNames(this.UserName, that.UserName) &&
				AreSamePasswords(this.Password, that.Password)
			);
		}

		public override int GetHashCode() {
			// counting in EndPoint, UserName and Password is sufficient 
			return this.EndPoint.GetHashCode() ^ this.UserName.GetHashCode() ^ this.Password.GetHashCode();
		}

		protected override MAPE.Utils.Settings Clone() {
			return new CredentialSettings(this);
		}

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
