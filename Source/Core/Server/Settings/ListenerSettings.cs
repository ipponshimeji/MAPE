using System;
using System.Diagnostics;
using System.Net;
using MAPE.Utils;


namespace MAPE.Server.Settings {
	public class ListenerSettings: MAPE.Utils.Settings {
		#region types

		public static class SettingNames {
			#region constants

			public const string Address = "Address";

			public const string Port = "Port";

			public const string Backlog = "Backlog";

			#endregion
		}

		public static class Defaults {
			#region constants

			public static readonly IPAddress Address = IPAddress.Loopback;

			public const int Port = 8888;

			public const int Backlog = 8;

			#endregion


			#region methods

			public static bool IsDefault(IPAddress address, int port, int backlog) {
				return backlog == Backlog && port == Port && address == Address;
			}

			#endregion
		}

		#endregion


		#region data

		private IPAddress address;

		private int port;

		private int backlog;

		#endregion


		#region properties

		public IPAddress Address {
			get {
				return this.address;
			}
			set {
				// argument checks
				if (value == null) {
					throw CreateArgumentNullException(nameof(value), SettingNames.Address);
				}

				this.address = value;
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

		public int Backlog {
			get {
				return this.backlog;
			}
			set {
				// argument checks
				if (value < 0) {
					throw new ArgumentOutOfRangeException(nameof(value), $"The '{SettingNames.Backlog}' value must be positive integer.");
				}

				this.backlog = value;
			}
		}

		public bool IsDefault {
			get {
				return Defaults.IsDefault(this.Address, this.Port, this.Backlog);
			}
		}

		#endregion


		#region creation and disposal

		public ListenerSettings(IObjectData data): base(data) {
			// prepare settings
			IPAddress address = Defaults.Address;
			int port = Defaults.Port;
			int backlog = Defaults.Backlog;
			if (data != null) {
				// get settings from data
				address = data.GetValue(SettingNames.Address, address, ExtractIPAddressValue);
				port = data.GetInt32Value(SettingNames.Port, port);
				backlog = data.GetInt32Value(SettingNames.Backlog, backlog);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.Address = address;
				this.Port = port;
				this.Backlog = backlog;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public ListenerSettings(): this(null) {
		}

		#endregion


		#region methods

		public bool HasSameEndpointTo(ListenerSettings that) {
			// argument checks
			if (that == null) {
				return false;
			}

			return this.Port == that.Port && this.Address == that.Address;
		}

		public IPEndPoint GetEndpoint() {
			return new IPEndPoint(this.Address, this.Port);
		}

		#endregion


		#region overrides

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// state checks
			Debug.Assert(this.Address != null);
			Debug.Assert(IPEndPoint.MinPort <= this.Port && this.Port <= IPEndPoint.MaxPort);
			Debug.Assert(0 <= this.Backlog);

			// save settings
			data.SetValue(SettingNames.Address, this.Address, CreateIPAddressValue);	// not omittable
			data.SetInt32Value(SettingNames.Port, this.Port);							// not omittable
			data.SetInt32Value(SettingNames.Backlog, this.Backlog, omitDefault, isDefault: this.Backlog == Defaults.Backlog);

			return;
		}

		#endregion


		#region private

		private static IPAddress ExtractIPAddressValue(IObjectDataValue value) {
			// argument checks
			Debug.Assert(value != null);

			// extract IPAddress value
			return IPAddress.Parse(value.ExtractStringValue());
		}

		private static IObjectDataValue CreateIPAddressValue(IObjectData data, IPAddress value) {
			// argument checks
			Debug.Assert(data != null);
			Debug.Assert(value != null);

			// set IPAddress value
			string stringValue = value.ToString();
			Debug.Assert(string.IsNullOrEmpty(stringValue));
			return data.CreateValue(stringValue);
		}

		#endregion
	}
}
