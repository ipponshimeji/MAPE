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
			using (Proxy proxy = new Proxy()) {
				proxy.Start();
				Console.WriteLine("Listening...");
				Console.WriteLine("Push any key to quit.");
				Console.ReadKey();
				proxy.Stop();
			}

			return;
		}

		#endregion
	}
}
