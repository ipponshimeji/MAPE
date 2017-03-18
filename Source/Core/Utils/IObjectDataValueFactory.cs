using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace MAPE.Utils {
	public interface IObjectDataValueFactory {
		IObjectDataValue CreateValue(long value);

		IObjectDataValue CreateValue(double value);

		IObjectDataValue CreateValue(bool value);

		IObjectDataValue CreateValue(string value);

		IObjectDataValue CreateValue(IObjectData value);

		IObjectDataValue CreateValue(IEnumerable<IObjectDataValue> value);
	}
}
