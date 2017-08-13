using System;
using Xunit;
using MAPE.Utils;


namespace MAPE.Test.Testing {
	public static class TestUtil {
		#region methods

		// ToDo: necessary?
		public static void AssertEqualLog(LogEntry expected, DateTime expectedBegin, DateTime expectedEnd, LogEntry actual) {
			Assert.True(
				expectedBegin <= actual.Time && actual.Time <= expectedEnd,
				$"Expected: between {expectedBegin} and {expectedEnd}{Environment.NewLine}Actual: {actual.Time}"
			);
			Assert.Equal(expected.ParentComponentId, actual.ParentComponentId);
			Assert.Equal(expected.ComponentId, actual.ComponentId);
			Assert.Equal(expected.ComponentName, actual.ComponentName);
			Assert.Equal(expected.EventType, actual.EventType);
			Assert.Equal(expected.Message, actual.Message);
			Assert.Equal(expected.EventId, actual.EventId);

			return;
		}

		#endregion
	}
}
