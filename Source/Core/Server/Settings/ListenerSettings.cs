using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
				return backlog == Backlog && port == Port && Address.Equals(address);
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

		public ListenerSettings(): this(NullObjectData) {
		}

		public ListenerSettings(ListenerSettings src) : base(src) {
			// argument checks
			if (src == null) {
				throw new ArgumentNullException(nameof(src));
			}

			// clone members
			this.Address = CloneIPAddress(src.Address);
			this.Port = src.Port;
			this.Backlog = src.Backlog;

			return;
		}

		public static ListenerSettings CreateDefaultListenerSettings(IEnumerable<ListenerSettings> existingList) {
			// argument checks
			// existingList can be null

			// create an instance with default settings
			ListenerSettings listenerSettings = new ListenerSettings();

			// select a port not to conflict with existing end points
			if (existingList != null) {
				int candidate = listenerSettings.Port;
				IPAddress address = listenerSettings.Address;
				int[] sortedExistingPorts = (
					from existingListenerSetting in existingList
					where address.Equals(existingListenerSetting.Address)
					select existingListenerSetting.Port
				).OrderBy(p => p).ToArray();

				// find the unused port
				foreach (int existingPort in sortedExistingPorts) {
					if (candidate < existingPort) {
						// candidate is not used
						break;
					} else if (candidate == existingPort) {
						// candidate is used
						++candidate;
					}
				}

				// set the unused port
				// an ArgumentOutOfRangeException is thrown if no unused port is found in the valid range
				listenerSettings.Port = candidate;
			}

			return listenerSettings;
		}

		#endregion


		#region methods

		public IPEndPoint GetEndPoint() {
			return new IPEndPoint(this.Address, this.Port);
		}

		public bool HasSameEndPointTo(IPEndPoint endPoint) {
			// argument checks
			if (endPoint == null) {
				return false;
			}

			return this.Port == endPoint.Port && this.Address.Equals(endPoint.Address);
		}

		public bool HasSameEndPointTo(ListenerSettings that) {
			// argument checks
			if (that == null) {
				return false;
			}

			return this.Port == that.Port && this.Address.Equals(that.Address);
		}

		public static ListenerSettings FindListenerSettingsOfSameEndPointTo(ListenerSettings settings, IEnumerable<ListenerSettings> collection) {
			// argument checks
			if (settings == null) {
				throw new ArgumentNullException(nameof(settings));
			}
			if (collection == null) {
				return null;
			}

			return collection.Where(s => settings.HasSameEndPointTo(s)).FirstOrDefault();
		}

		#endregion


		#region overrides

		protected override MAPE.Utils.Settings Clone() {
			return new ListenerSettings(this);
		}

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
			Debug.Assert(string.IsNullOrEmpty(stringValue) == false);
			return data.CreateValue(stringValue);
		}

		private static IPAddress CloneIPAddress(IPAddress src) {
			// argument checks
			if (src == null) {
				return null;
			}

			return new IPAddress(src.GetAddressBytes());
		}

		#endregion
	}
}
