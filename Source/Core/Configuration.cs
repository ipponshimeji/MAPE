using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;


namespace MAPE.Core {
	public class Configuration {
		#region types

		public static class Names {
			#region constants

			public const string Proxy = "Proxy";

			public const string MainListener = "MainListener";

			public const string AdditionalListeners = "AdditionalListeners";

			#endregion
		}

		public struct Option {
			#region data

			public readonly string Name;

			public readonly string Value;

			#endregion


			#region creation and disposal

			public Option(string name, string value) {
				// initialize members
				this.Name = name;
				this.Value = value;

				return;
			}

			#endregion


			#region methods

			public static bool AreEqualNames(string name1, string name2) {
				return string.Compare(name1, name2, StringComparison.InvariantCultureIgnoreCase) == 0;
			}

			public bool IsEqualName(string name) {
				return AreEqualNames(this.Name, name);
			}

			#endregion
		}

		#endregion


		#region data

		protected IPEndPoint proxy;

		protected ListenerConfiguration mainListener;

		protected ListenerConfiguration[] additionalListeners;

		#endregion


		#region creation and disposal

		public Configuration(bool suppressReadingConfig = false) {
			// initialize members
			this.proxy = null;
			this.mainListener = null;
			this.additionalListeners = null;

			return;
		}

		#endregion


		#region methods
		#endregion


		#region overridables

		public virtual bool HandleOption(Option option) {
			if (option.IsEqualName(Names.Proxy)) {
				this.proxy = null;
				return true;
			} else {
				return false;   // not handled
			}
		}

		#endregion


	}
}
