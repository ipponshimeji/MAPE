using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace MAPE.ComponentBase {
	public abstract class TaskingComponent: Component {
		#region data - synchronized by locking this

		private Task task = null;

		#endregion


		#region properties

		public Task Task {
			get {
				Task value;
				lock (this) {
					value = this.task;
					if (value != null && value.IsCompleted) {
						// the task has been completed
						// no need to keep the object
						this.task = null;
						value = null;
					}
				}

				return value;
			}
			protected set {
				lock (this) {
					this.task = value;
				}
			}
		}

		#endregion


		#region creation and disposal

		protected TaskingComponent() {
		}

		#endregion


		#region methods

		public static Task[] GetActiveTaskList(IEnumerable<TaskingComponent> components) {
			// argument checks
			if (components == null) {
				throw new ArgumentNullException(nameof(components));
			}

			return components.Select(c => c.Task).Where(t => t != null).ToArray();
		}

		#endregion
	}
}
