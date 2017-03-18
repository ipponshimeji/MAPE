using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;


namespace MAPE.Utils.Test {
	public class JsonObjectDataTest {
		#region data

		public static Func<IObjectData> CreateEmpty = () => {
			return JsonObjectData.CreateEmpty();
		};

		#endregion


		#region test - Value

		public class BasicProtocol: ObjectDataTestBase.BasicProtocolTestBase {
			#region creation and disposal

			public BasicProtocol(): base(CreateEmpty) {
			}

			#endregion
		}

		#endregion


#if N
		#region test int

		public class Int32Value: ObjectDataTestBase.Int32ValueTestBase {
		#region creation and disposal

			public Int32Value(): base(CreateEmptyJsonObjectData) {
			}

		#endregion
		}

		#endregion


		#region test bool

		public class BooleanValue: ObjectDataTestBase.BooleanValueTestBase {
		#region creation and disposal

			public BooleanValue() : base(CreateEmptyJsonObjectData) {
			}

		#endregion
		}

		#endregion


		#region test string

		public class StringValue: ObjectDataTestBase.StringValueTestBase {
		#region creation and disposal

			public StringValue() : base(CreateEmptyJsonObjectData) {
			}

		#endregion
		}

		#endregion


		#region test object

		public class ObjectValue: ObjectDataTestBase.ObjectValueTestBase {
		#region creation and disposal

			public ObjectValue() : base(CreateEmptyJsonObjectData) {
			}

		#endregion
		}

		#endregion


		#region test Enum

		public class EnumValue: ObjectDataTestBase.EnumValueTestBase {
		#region creation and disposal

			public EnumValue(): base(CreateEmptyJsonObjectData) {
			}

		#endregion
		}

		#endregion


		#region test general object (value type)

		public class DateTimeValue: ObjectDataExtensionTestBase.DateTimeValueTestBase {
		#region creation and disposal

			public DateTimeValue() : base(CreateEmptyJsonObjectData) {
			}

		#endregion
		}

		#endregion


		#region test general object (reference type)

		public class VersionValue: ObjectDataExtensionTestBase.VersionValueTestBase {
		#region creation and disposal

			public VersionValue() : base(CreateEmptyJsonObjectData) {
			}

		#endregion
		}

		#endregion


		#region test int[]

		public class Int32ArrayValue: ObjectDataTestBase.Int32ArrayValueTestBase {
		#region creation and disposal

			public Int32ArrayValue() : base(CreateEmptyJsonObjectData) {
			}

		#endregion
		}

		#endregion


		#region test bool[]

		public class BooleanArrayValue: ObjectDataTestBase.BooleanArrayValueTestBase {
		#region creation and disposal

			public BooleanArrayValue() : base(CreateEmptyJsonObjectData) {
			}

		#endregion
		}

		#endregion


		#region test string[]

		public class StringArrayValue: ObjectDataTestBase.StringArrayValueTestBase {
		#region creation and disposal

			public StringArrayValue() : base(CreateEmptyJsonObjectData) {
			}

		#endregion
		}

		#endregion
#endif
	}
}
