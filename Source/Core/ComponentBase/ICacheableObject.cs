using System;


namespace MAPE.ComponentBase {
	public interface ICacheableObject {
		void OnCaching();

		void OnDecached();
	}

	public interface ICacheableObject<TInitParam> {
		void OnCaching();

		void OnDecached(TInitParam initParam);
	}
}
