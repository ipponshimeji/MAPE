using System;
using System.Diagnostics;
using System.Net;
using MAPE.Utils;


namespace MAPE.Command.Settings {
	public class ActualProxySettings: MAPE.Utils.Settings {
		#region types

		public static class SettingNames {
			#region constants

			public const string Host = "Host";

			public const string Port = "Port";

			#endregion
		}

		public static class Defaults {
			#region constants

			public const int Port = 80;     // default http port

			#endregion
		}

		#endregion


		#region data

		private string host;

		private int port;

		#endregion


		#region properties

		public string Host {
			get {
				return this.host;
			}
			set {
				// argument checks
				if (string.IsNullOrEmpty(value)) {
					throw CreateArgumentNullOrEmptyException(nameof(value), SettingNames.Host);
				}

				this.host = value;
			}
		}

		public int Port {
			get {
				return this.port;
			}
			set {
				// argument checks
				if (value < IPEndPoint.MinPort || IPEndPoint.MaxPort < value) {
					throw new ArgumentOutOfRangeException(nameof(value), $"The '{SettingNames.Port}' value must be between {IPEndPoint.MinPort} and {IPEndPoint.MaxPort}, inclusive.");
				}

				this.port = value;
			}
		}

		#endregion


		#region creation and disposal

		public ActualProxySettings(IObjectData data): base(data) {
			// prepare settings
			string host = null;
			int port = Defaults.Port;
			if (data != null) {
				// get settings from data
				host = data.GetStringValue(SettingNames.Host, host);
				IObjectDataValue portValue = data.GetValue(SettingNames.Port);
				if (portValue == null) {
					throw CreateMissingIndispensableSettingException(SettingNames.Port);
				}
				port = portValue.ExtractInt32Value();
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.Host = host;
				this.Port = port;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public ActualProxySettings(): this(NullObjectData) {
		}

		public ActualProxySettings(ActualProxySettings src): base(src) {
			// argument checks
			if (src == null) {
				throw new ArgumentNullException(nameof(src));
			}

			// clone members
			this.host = src.host;
			this.port = src.port;

			return;
		}
	
		#endregion


		#region methods

		public WebProxy CreateWebProxy() {
				// state checks
				EnsureHostIsValid();
				Debug.Assert(string.IsNullOrEmpty(this.Host) == false);
				Debug.Assert(IPEndPoint.MinPort <= this.Port && this.Port <= IPEndPoint.MaxPort);

				// create a WebProxy object
				return new WebProxy(this.Host, this.Port);
			}

		#endregion


		#region overrides

		protected override MAPE.Utils.Settings Clone() {
			return new ActualProxySettings(this);
		}

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// state checks
			EnsureHostIsValid();
			Debug.Assert(string.IsNullOrEmpty(this.Host) == false);
			Debug.Assert(IPEndPoint.MinPort <= this.Port && this.Port <= IPEndPoint.MaxPort);

			// save settings (these settings are not omittable)
			data.SetStringValue(SettingNames.Host, this.Host);
			data.SetInt32Value(SettingNames.Port, this.Port);

			return;
		}

		#endregion


		#region privates

		private void EnsureHostIsValid() {
			// state checks
			if (string.IsNullOrEmpty(this.Host)) {
				throw CreateMissingIndispensableSettingException(SettingNames.Host);
			}

			return;
		}

		#endregion
	}
}
