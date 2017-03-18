using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace MAPE.Utils {
	public interface IObjectDataValue {
		ObjectDataValueType Type { get; }

		int ExtractInt32Value();

		long ExtractInt64Value();

		double ExtractDoubleValue();

		bool ExtractBooleanValue();

		string ExtractStringValue();

		IObjectData ExtractObjectValue();

		IEnumerable<IObjectDataValue> ExtractArrayValue();
	}
}
