using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using MAPE.Utils;


namespace MAPE.Server.Settings {
	public class ProxySettings: MAPE.Utils.Settings {
		#region types

		public static class SettingNames {
			#region constants

			public const string MainListener = "MainListener";

			public const string AdditionalListeners = "AdditionalListeners";

			public const string RetryCount = "RetryCount";

			#endregion
		}

		public static class Defaults {
			#region constants

			public static readonly ListenerSettings[] AdditionalListeners = null;

			public const int RetryCount = 2;     // original try + 2 retries = 3 tries

			#endregion


			#region methods

			public static ListenerSettings CreateDefaultMainListener() {
				return new ListenerSettings();
			}

			public static bool IsDefaultMainListener(ListenerSettings value) {
				return value != null && value.IsDefault;
			}

			public static bool IsDefaultAdditionalListeners(IEnumerable<ListenerSettings> value) {
				return value == null || value.Count() == 0;
			}

			#endregion
		}

		#endregion


		#region data

		private ListenerSettings mainListener;

		private IEnumerable<ListenerSettings> additionalListeners;

		private int retryCount;

		#endregion


		#region properties

		public ListenerSettings MainListener {
			get {
				return this.mainListener;
			}
			set {
				// argument checks
				if (value == null) {
					throw CreateArgumentNullException(nameof(value), SettingNames.MainListener);
				}
				if (this.AdditionalListeners != null) {
					IPEndPoint conflict = FindConflictingEndPoint(value, this.AdditionalListeners);
					if (conflict != null) {
						throw new ArgumentOutOfRangeException(nameof(value), $"The end point of the '{SettingNames.MainListener}', '{conflict}', conflicts with one in the '{SettingNames.AdditionalListeners}'.");
					}
				}

				this.mainListener = value;
			}
		}

		public IEnumerable<ListenerSettings> AdditionalListeners {
			get {
				return this.additionalListeners;
			}
			set {
				// argument checks
				// value can be null
				if (value != null) {
					IPEndPoint conflict = FindConflictingEndPoint(value);
					if (conflict != null) {
						throw new ArgumentOutOfRangeException(nameof(value), $"The '{SettingNames.AdditionalListeners}' contain a conflicting end point '{conflict}'.");
					}

					conflict = FindConflictingEndPoint(this.MainListener, value);
					if (conflict != null) {
						throw new ArgumentOutOfRangeException(nameof(value), $"The end point '{conflict}' in the '{SettingNames.AdditionalListeners}' conflicts with one of the '{SettingNames.MainListener}'.");
					}
				}

				this.additionalListeners = value;
			}
		}

		public int RetryCount {
			get {
				return this.retryCount;
			}
			set {
				// argument checks
				if (value < 0) {
					throw new ArgumentOutOfRangeException(nameof(value), $"The '{SettingNames.RetryCount}' value must be positive.");
				}

				this.retryCount = value;
			}
		}

		#endregion


		#region creation and disposal

		public ProxySettings(IObjectData data) : base(data) {
			// prepare settings
			ListenerSettings mainListener = null;
			IEnumerable<ListenerSettings> additionalListeners = null;
			int retryCount = Defaults.RetryCount;
			if (data != null) {
				// get settings from data
				mainListener = data.GetObjectValue(SettingNames.MainListener, mainListener, CreateListenerSettings);
				additionalListeners = data.GetObjectArrayValue(SettingNames.AdditionalListeners, additionalListeners, CreateListenerSettings);
				retryCount = data.GetInt32Value(SettingNames.RetryCount, retryCount);
			}
			if (mainListener == null) {
				mainListener = Defaults.CreateDefaultMainListener();
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.MainListener = mainListener;
				this.AdditionalListeners = additionalListeners;
				this.RetryCount = retryCount;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public ProxySettings(): this(NullObjectData) {
		}

		public ProxySettings(ProxySettings src): base(src) {
			// argument checks
			if (src == null) {
				throw new ArgumentNullException(nameof(src));
			}

			// clone members
			this.MainListener = Clone(src.MainListener);
			this.AdditionalListeners = Clone(src.AdditionalListeners);
			this.RetryCount = src.RetryCount;

			return;
		}

		#endregion


		#region meethods

		protected static ListenerSettings CreateListenerSettings(IObjectData data) {
			// argument checks
			Debug.Assert(data != null);

			return new ListenerSettings(data);
		}

		protected static IPEndPoint FindConflictingEndPoint(ListenerSettings newListener, IEnumerable<ListenerSettings> existingListeners) {
			// argument checks
			if (newListener == null || existingListeners == null) {
				return null;
			}

			ListenerSettings listenerSettings = ListenerSettings.FindListenerSettingsOfSameEndPointTo(newListener, existingListeners);
			return listenerSettings?.GetEndPoint();
		}

		protected static IPEndPoint FindConflictingEndPoint(IEnumerable<ListenerSettings> listeners) {
			// argument checks
			if (listeners == null) {
				return null;
			}

			List<ListenerSettings> buf = new List<ListenerSettings>();
			foreach (ListenerSettings listener in listeners) {
				IPEndPoint endPoint = FindConflictingEndPoint(listener, buf);
				if (endPoint != null) {
					return endPoint;
				}
				buf.Add(listener);
			}

			return null;    // no conflict
		}

		public List<ListenerSettings> GetListeners() {
			List<ListenerSettings> listeners = new List<ListenerSettings>();
			listeners.Add(this.MainListener);
			if (this.AdditionalListeners != null) {
				listeners.AddRange(this.AdditionalListeners);
			}

			return listeners;
		}

		#endregion


		#region overrides

		protected override MAPE.Utils.Settings Clone() {
			return new ProxySettings(this);
		}

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// state checks
			Debug.Assert(this.MainListener != null);
			// this.AdditionalListeners can be null
			Debug.Assert(0 < this.RetryCount);

			// save settings
			data.SetObjectValue(SettingNames.MainListener, this.MainListener, false, omitDefault, Defaults.IsDefaultMainListener(this.MainListener));
			data.SetObjectArrayValue(SettingNames.AdditionalListeners, this.AdditionalListeners, omitDefault, Defaults.IsDefaultAdditionalListeners(this.AdditionalListeners));
			data.SetInt32Value(SettingNames.RetryCount, this.RetryCount, omitDefault, this.RetryCount == Defaults.RetryCount);

			return;
		}

		#endregion
	}
}
