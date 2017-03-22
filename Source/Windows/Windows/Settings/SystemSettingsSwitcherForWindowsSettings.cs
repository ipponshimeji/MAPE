using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MAPE.Utils;
using MAPE.Command.Settings;


namespace MAPE.Windows.Settings {
	public class SystemSettingsSwitcherForWindowsSettings: SystemSettingsSwitcherSettings {
		#region types

		public static new class SettingNames {
			#region constants

			public const string ProxyOverride = "ProxyOverride";

			#endregion
		}

		public static new class Defaults {
			#region constants

			public const string ProxyOverride = "";

			#endregion
		}

		#endregion


		#region constants

		public const char ProxyOverrideSeparatorChar = ';';

		public const string LocalFragment = "<local>";

		#endregion


		#region data

		private string proxyOverride = string.Empty;


		// caches

		private bool bypassLocal = false;

		private string filteredProxyOverride = string.Empty;

		#endregion


		#region properties

		public string ProxyOverride {
			get {
				return this.proxyOverride;
			}
			set {
				// argument checks
				if (value == null) {
					value = string.Empty;
				}
				if (AreSameProxyOverrideValues(this.proxyOverride, value) == false) {
					// do not set values through property accessors not to be updated redundantly
					this.filteredProxyOverride = FilterLocalFragment(value, out this.bypassLocal);
					this.proxyOverride = value;
				}
			}
		}

		public bool BypassLocal {
			get {
				return this.bypassLocal;
			}
			set {
				// argument checks
				if (this.bypassLocal != value) {
					// do not set values through property accessors not to be updated redundantly
					if (value) {
						this.proxyOverride = AppendLocalFragment(this.proxyOverride);
					} else {
						bool dummy;
						this.proxyOverride = FilterLocalFragment(this.proxyOverride, out dummy);
					}
					this.bypassLocal = value;
				}
			}
		}

		public string FilteredProxyOverride {
			get {
				return this.filteredProxyOverride;
			}
			set {
				// argument checks
				if (value == null) {
					value = string.Empty;
				}
				if (AreSameProxyOverrideValues(this.filteredProxyOverride, value) == false) {
					// do not set values through property accessors not to be updated redundantly
					this.filteredProxyOverride = FilterLocalFragment(value, out this.bypassLocal);
					if (this.bypassLocal) {
						this.proxyOverride = AppendLocalFragment(this.filteredProxyOverride);
					} else {
						this.proxyOverride = this.filteredProxyOverride;
					}

					this.proxyOverride = value;
				}
			}
		}

		#endregion


		#region creation and disposal

		public SystemSettingsSwitcherForWindowsSettings(IObjectData data): base(data) {
			// prepare settings
			string proxyOverride = Defaults.ProxyOverride;
			if (data != null) {
				// get settings from data
				proxyOverride = data.GetStringValue(SettingNames.ProxyOverride, proxyOverride);
			}

			// set settings
			try {
				// may throw ArgumentException for an invalid value
				this.ProxyOverride = proxyOverride;
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return;
		}

		public SystemSettingsSwitcherForWindowsSettings() : this(NullObjectData) {
		}

		public SystemSettingsSwitcherForWindowsSettings(SystemSettingsSwitcherForWindowsSettings src) : base(src) {
			// argument checks
			if (src == null) {
				throw new ArgumentNullException(nameof(src));
			}

			// clone members
			this.ProxyOverride = src.ProxyOverride;

			return;
		}

		#endregion


		#region methods

		public static bool AreSameProxyOverrideValues(string value1, string value2) {
			return string.Compare(value1, value2, StringComparison.OrdinalIgnoreCase) == 0;
		}

		public static bool IsLocalFragment(string fragment) {
			return AreSameProxyOverrideValues(fragment, LocalFragment);
		}

		#endregion


		#region overridables

		protected override MAPE.Utils.Settings Clone() {
			return new SystemSettingsSwitcherForWindowsSettings(this);
		}

		protected override void SaveTo(IObjectData data, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);

			// save the base class level settings
			base.SaveTo(data, omitDefault);

			// save this class level settings
			data.SetStringValue(SettingNames.ProxyOverride, this.ProxyOverride, omitDefault, this.ProxyOverride == Defaults.ProxyOverride);

			return;
		}

		#endregion


		#region privates

		private static string FilterLocalFragment(string proxyOverride, out bool bypassLocal) {
			// argument checks
			// proxyOverride can be null

			// filter LocalFragment
			bool hasLocal = false;
			string filteredProxyOverride;
			if (string.IsNullOrEmpty(proxyOverride)) {
				filteredProxyOverride = string.Empty;
			} else {
				Func<string, bool> filter = (fragment) => {
					if (IsLocalFragment(fragment)) {
						hasLocal = true;
						return false;
					} else {
						return true;
					}
				};

				IEnumerable<string> fragments = (
					from fragment in proxyOverride.Split(ProxyOverrideSeparatorChar)
					where filter(fragment)
					select fragment
				);
				filteredProxyOverride = string.Join(ProxyOverrideSeparatorChar.ToString(), fragments);
			}

			// return the result
			bypassLocal = hasLocal;
			return filteredProxyOverride;
		}

		private static string AppendLocalFragment(string proxyOverride) {
			// argument checks
			// proxyOverride can be null

			return string.IsNullOrEmpty(proxyOverride)? LocalFragment: string.Concat(proxyOverride, ProxyOverrideSeparatorChar.ToString(), LocalFragment);
		}

		#endregion
	}
}
