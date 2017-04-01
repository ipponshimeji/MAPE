using System;
using System.Diagnostics;
using System.Net;
using MAPE.Utils;
using MAPE.Server;
using MAPE.Command.Settings;


namespace MAPE.Command {
	public class SystemSettings: MAPE.Utils.Settings {
		#region creation and disposal

		public SystemSettings(IObjectData data): base(data) {
		}

		public SystemSettings(): this(NullObjectData) {
		}

		public SystemSettings(SystemSettings src) : base(src) {
		}

		#endregion


		#region overrides/overridables

		protected override MAPE.Utils.Settings Clone() {
			return new SystemSettings(this);
		}

		protected override void SaveTo(IObjectData data, bool omitDefault) {
		}

		#endregion
	}
}
