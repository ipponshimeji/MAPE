using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using MAPE.Test.Testing;


namespace MAPE.Utils.Test {
	public class DisposableUtilTest {
		#region types

		public class SampleDisposable: IDisposable {
			#region data

			private bool throwException;

			public int DisposedCount { get; private set; }

			#endregion


			#region creation & disposal

			public SampleDisposable(bool throwException = false) {
				// initialize members
				this.throwException = throwException;
				this.DisposedCount = 0;

				return;
			}

			public void Dispose() {
				++this.DisposedCount;
				if (throwException) {
					throw new Exception("Error in Dispose().");
				}
			}

			#endregion
		}

		#endregion


		#region test - DisposeSuppressingErrors

		public class DisposeSuppressingErrors {
			#region tests

			[Fact(DisplayName = "general")]
			public void General() {
				// ARRANGE
				SampleDisposable target = new SampleDisposable();
				TestLogMonitor logMonitor = new TestLogMonitor();

				// ACT
				logMonitor.StartLogging();
				try {
					DisposableUtil.DisposeSuppressingErrors(target);
				} finally {
					logMonitor.StopLogging();
				}

				// ASSERT
				Assert.Equal(1, target.DisposedCount);
				Assert.Equal(0, logMonitor.LogCount);
			}

			[Fact(DisplayName = "call in extension method form")]
			public void ExtensionForm() {
				// ARRANGE
				SampleDisposable target = new SampleDisposable();
				TestLogMonitor logMonitor = new TestLogMonitor();

				// ACT
				logMonitor.StartLogging();
				try {
					target.DisposeSuppressingErrors();
				} finally {
					logMonitor.StopLogging();
				}

				// ASSERT
				Assert.Equal(1, target.DisposedCount);
				Assert.Equal(0, logMonitor.LogCount);
			}

			[Fact(DisplayName = "error")]
			public void Error() {
				// ARRANGE
				SampleDisposable target = new SampleDisposable(throwException: true);
				TestLogMonitor logMonitor = new TestLogMonitor();

				// ACT
				logMonitor.StartLogging();
				try {
					DisposableUtil.DisposeSuppressingErrors(target);
				} finally {
					logMonitor.StopLogging();
				}

				// ASSERT
				Assert.Equal(1, target.DisposedCount);
				Assert.Equal(1, logMonitor.LogCount);
				LogEntry expectedEntry = new LogEntry(null, TraceEventType.Error, "Fail to dispose the object at 'DisposeSuppressingErrors.Error()': Error in Dispose().");
				logMonitor.AssertEqualLog(expectedEntry, actualIndex: 0);
			}

			[Fact(DisplayName = "target: null")]
			public void Args_target_null() {
				// ARRANGE
				SampleDisposable target = null;

				// ACT
				DisposableUtil.DisposeSuppressingErrors(target);

				// ASSERT
				// expected no ArgumentNullException
			}

			[Fact(DisplayName = "errorLogTemplate: valid")]
			public void Args_errorLogTemplate_valid() {
				// ARRANGE
				SampleDisposable target = new SampleDisposable(throwException: true);
				string errorLogTemplate = "oops! at '{1}': {0}";	// valid template
				TestLogMonitor logMonitor = new TestLogMonitor();

				// ACT
				logMonitor.StartLogging();
				try {
					DisposableUtil.DisposeSuppressingErrors(target, errorLogTemplate);
				} finally {
					logMonitor.StopLogging();
				}

				// ASSERT
				Assert.Equal(1, target.DisposedCount);
				Assert.Equal(1, logMonitor.LogCount);
				LogEntry expectedEntry = new LogEntry(null, TraceEventType.Error, "oops! at 'DisposeSuppressingErrors.Args_errorLogTemplate_valid()': Error in Dispose().");
				logMonitor.AssertEqualLog(expectedEntry, actualIndex: 0);
			}

			[Fact(DisplayName = "errorLogTemplate: invalid")]
			public void Args_errorLogTemplate_invalid() {
				// ARRANGE
				SampleDisposable target = new SampleDisposable(throwException: true);
				string errorLogTemplate = "oops! at '{1}': {0} {2}";    // invalid template (too many params)
				TestLogMonitor logMonitor = new TestLogMonitor();

				// ACT
				logMonitor.StartLogging();
				try {
					DisposableUtil.DisposeSuppressingErrors(target, errorLogTemplate);
				} finally {
					logMonitor.StopLogging();
				}

				// ASSERT
				Assert.Equal(1, target.DisposedCount);
				Assert.Equal(1, logMonitor.LogCount);
				LogEntry expectedEntry = new LogEntry(null, TraceEventType.Error, "Fail to dispose the object: Error in Dispose().");
				logMonitor.AssertEqualLog(expectedEntry, actualIndex: 0);
			}

			#endregion
		}

		#endregion


		#region test - ClearDisposableObject

		public class ClearDisposableObject {
			#region tests

			[Fact(DisplayName = "general")]
			public void General() {
				// ARRANGE
				SampleDisposable sample = new SampleDisposable();
				SampleDisposable target = sample;
				TestLogMonitor logMonitor = new TestLogMonitor();

				// ACT
				logMonitor.StartLogging();
				try {
					DisposableUtil.ClearDisposableObject(ref target);
				} finally {
					logMonitor.StopLogging();
				}

				// ASSERT
				Assert.Equal(null, target);
				Assert.Equal(1, sample.DisposedCount);
				Assert.Equal(0, logMonitor.LogCount);
			}

			[Fact(DisplayName = "error")]
			public void Error() {
				// ARRANGE
				SampleDisposable sample = new SampleDisposable(throwException: true);
				SampleDisposable target = sample;
				TestLogMonitor logMonitor = new TestLogMonitor();

				// ACT
				logMonitor.StartLogging();
				try {
					DisposableUtil.ClearDisposableObject(ref target);
				} finally {
					logMonitor.StopLogging();
				}

				// ASSERT
				Assert.Equal(null, target);
				Assert.Equal(1, sample.DisposedCount);
				Assert.Equal(1, logMonitor.LogCount);
				LogEntry expectedEntry = new LogEntry(null, TraceEventType.Error, "Fail to dispose the object at 'ClearDisposableObject.Error()': Error in Dispose().");
				logMonitor.AssertEqualLog(expectedEntry, actualIndex: 0);
			}

			[Fact(DisplayName = "target: null")]
			public void Args_target_null() {
				// ARRANGE
				SampleDisposable target = null;

				// ACT
				DisposableUtil.ClearDisposableObject(ref target);

				// ASSERT
				// expected no ArgumentNullException
				Assert.Equal(null, target);
			}

			[Fact(DisplayName = "errorLogTemplate: valid")]
			public void Args_errorLogTemplate_valid() {
				// ARRANGE
				SampleDisposable sample = new SampleDisposable(throwException: true);
				SampleDisposable target = sample;
				string errorLogTemplate = "oops! at '{1}': {0}";    // valid template
				TestLogMonitor logMonitor = new TestLogMonitor();

				// ACT
				logMonitor.StartLogging();
				try {
					DisposableUtil.ClearDisposableObject(ref target, errorLogTemplate);
				} finally {
					logMonitor.StopLogging();
				}

				// ASSERT
				Assert.Equal(null, target);
				Assert.Equal(1, sample.DisposedCount);
				Assert.Equal(1, logMonitor.LogCount);
				LogEntry expectedEntry = new LogEntry(null, TraceEventType.Error, "oops! at 'ClearDisposableObject.Args_errorLogTemplate_valid()': Error in Dispose().");
				logMonitor.AssertEqualLog(expectedEntry, actualIndex: 0);
			}

			[Fact(DisplayName = "errorLogTemplate: invalid")]
			public void Args_errorLogTemplate_invalid() {
				// ARRANGE
				SampleDisposable sample = new SampleDisposable(throwException: true);
				SampleDisposable target = sample;
				string errorLogTemplate = "oops! at '{1}': {0} {2}";    // invalid template (too many params)
				TestLogMonitor logMonitor = new TestLogMonitor();

				// ACT
				logMonitor.StartLogging();
				try {
					DisposableUtil.ClearDisposableObject(ref target, errorLogTemplate);
				} finally {
					logMonitor.StopLogging();
				}

				// ASSERT
				Assert.Equal(null, target);
				Assert.Equal(1, sample.DisposedCount);
				Assert.Equal(1, logMonitor.LogCount);
				LogEntry expectedEntry = new LogEntry(null, TraceEventType.Error, "Fail to dispose the object: Error in Dispose().");
				logMonitor.AssertEqualLog(expectedEntry, actualIndex: 0);
			}

			#endregion
		}

		#endregion
	}
}
