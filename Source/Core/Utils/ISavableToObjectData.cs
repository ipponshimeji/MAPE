using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace MAPE.Utils {
	public interface ISavableToObjectData {
		void SaveToObjectData(IObjectData data, bool omitDefault);
	}
}
