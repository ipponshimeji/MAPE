using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace MAPE.Utils {
	public interface IObjectData: IEquatable<IObjectData>, IObjectDataValueFactory {
		IEnumerable<string> GetNames();

		IObjectDataValue GetValue(string name);

		void SetValue(string name, IObjectDataValue value);

		bool RemoveValue(string name);

		IObjectData CreateObject();
	}
}
