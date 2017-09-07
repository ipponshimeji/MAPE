using System;


namespace MAPE.ComponentBase {
	public interface ICacheableObject {
		void OnCaching();

		void OnDecached();
	}
}
