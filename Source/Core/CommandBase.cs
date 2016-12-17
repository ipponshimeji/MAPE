using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MAPE.Core {
    public class CommandBase: IDisposable {
		#region data
		#endregion


		#region creation and disposal

		public CommandBase() {
			return;
		}

		public virtual void Dispose() {
			// clear the cache
			return;
		}

		#endregion


		#region methods

		public void Run(string[] args) {
			using (Proxy proxy = new Proxy(null)) {
				proxy.Start();
				Console.WriteLine("Listening...");
				Console.WriteLine("Push any key to quit.");
				Console.ReadKey();
				bool b = proxy.Stop(5000);
				Console.WriteLine(b? "Completed.": "Not Completed.");
			}

			return;
		}

		#endregion
	}
}
