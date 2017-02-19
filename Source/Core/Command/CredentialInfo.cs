using System;
using System.Diagnostics;
using System.Net;


namespace MAPE.Command {
	public class CredentialInfo {
		#region data

		private readonly NetworkCredential credential;

		public readonly CredentialPersistence Persistence;

		public readonly bool EnableAssumptionMode;

		#endregion


		#region properties

		public string EndPoint {
			get {
				// Note that the endPoint is stored as 'Domain' property of the NetworkCredential object.
				return this.credential.Domain;
			}
		}

		public string UserName {
			get {
				return this.credential.UserName;
			}
		}

		public string Password {
			get {
				return this.credential.Password;
			}
		}

		#endregion


		#region creation and disposal

		public CredentialInfo(string endPoint, string userName, string password, CredentialPersistence persistence, bool enableAssumptionMode) {
			// argument checks
			if (endPoint == null) {
				// endPoint cannot be null, but can be empty
				throw new ArgumentNullException(nameof(endPoint));
			}
			// userName can be null
			// password can be null

			// initialize members
			// Note that the endPoint is stored as 'Domain' property of the NetworkCredential object.
			this.credential = new NetworkCredential(userName, password, endPoint);
			this.Persistence = persistence;
			this.EnableAssumptionMode = enableAssumptionMode;

			return;
		}

		public CredentialInfo Clone() {
			return new CredentialInfo(this.EndPoint, this.UserName, this.Password, this.Persistence, this.EnableAssumptionMode);
		}

		#endregion


		#region methods

		public static bool AreSameEndPoint(string endPoint1, string endPoint2) {
			// case-insensitive
			return string.Compare(endPoint1, endPoint2, StringComparison.OrdinalIgnoreCase) == 0;
		}

		public NetworkCredential GetNetworkCredential() {
			// return a clone of this.credential not to be changed its contents
			NetworkCredential credential = this.credential;
			return new NetworkCredential(credential.UserName, credential.Password, credential.Domain);
		}

		#endregion


		#region overrides

		public override bool Equals(object obj) {
			// argument checks
			CredentialInfo another = obj as CredentialInfo;
			if (another == null) {
				return false;
			}

			return (
				this.EnableAssumptionMode == another.EnableAssumptionMode &&
				this.Persistence == another.Persistence &&
				AreSameEndPoint(this.EndPoint, another.EndPoint) &&
				string.CompareOrdinal(this.UserName, another.UserName) == 0 &&
				string.CompareOrdinal(this.Password, another.Password) == 0
			);
		}

		public override int GetHashCode() {
			return this.EnableAssumptionMode.GetHashCode() ^ this.credential.GetHashCode() ^ this.Persistence.GetHashCode();
		}

		#endregion
	}
}
