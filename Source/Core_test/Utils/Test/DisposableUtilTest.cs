using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Xunit;
using MAPE.Testing;


namespace MAPE.Utils.Test {
	public class DisposableUtilTest {
		#region types

		public class SampleDisposable: IDisposable {
			#region data

			private static int lastId = 0;


			private readonly int id;

			private readonly bool throwException;

			public int DisposedCount { get; private set; }

			#endregion


			#region properties

			public string DisposeErrorMessage {
				get {
					return this.throwException ? $"Error from {this.id}" : null;
				}
			}

			#endregion


			#region creation & disposal

			public SampleDisposable(bool throwException = false) {
				// allocate id
				int id = Interlocked.Increment(ref SampleDisposable.lastId);

				// initialize members
				this.id = id;
				this.throwException = throwException;
				this.DisposedCount = 0;

				return;
			}

			public void Dispose() {
				++this.DisposedCount;
				if (throwException) {
					string message = this.DisposeErrorMessage;
					Debug.Assert(message != null);
					throw new Exception(message);
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
				SampleDisposable arg = new SampleDisposable();

				// ACT
				DisposableUtil.DisposeSuppressingErrors(arg);

				// ASSERT
				Assert.Equal(1, arg.DisposedCount);
			}

			[Fact(DisplayName = "general: call as extension method")]
			public void General_AsExtensionMethod() {
				// ARRANGE
				SampleDisposable arg = new SampleDisposable();

				// ACT
				arg.DisposeSuppressingErrors();

				// ASSERT
				Assert.Equal(1, arg.DisposedCount);
			}

			[Fact(DisplayName = "error")]
			public void Error() {
				// ARRANGE
				SampleDisposable arg = new SampleDisposable(throwException: true);
				TestLogMonitor logMonitor = new TestLogMonitor();

				// ACT
				logMonitor.StartLogging();
				try {
					DisposableUtil.DisposeSuppressingErrors(arg);
					// no exception expected
				} finally {
					logMonitor.StopLogging();
				}

				// ASSERT
				Assert.Equal(1, arg.DisposedCount);
				LogEntry expectedEntry = new LogEntry(null, TraceEventType.Error, $"Fail to dispose the object at 'DisposeSuppressingErrors.Error()': {arg.DisposeErrorMessage}");
				logMonitor.AssertContains(expectedEntry);
			}

			[Fact(DisplayName = "target: null")]
			public void Args_target_null() {
				// ARRANGE
				SampleDisposable arg = null;

				// ACT
				DisposableUtil.DisposeSuppressingErrors(arg);

				// ASSERT
				// no ArgumentNullException expected
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
				SampleDisposable arg = sample;

				// ACT
				DisposableUtil.ClearDisposableObject(ref arg);

				// ASSERT
				Assert.Equal(null, arg);
				Assert.Equal(1, sample.DisposedCount);
			}

			[Fact(DisplayName = "error")]
			public void Error() {
				// ARRANGE
				SampleDisposable sample = new SampleDisposable(throwException: true);
				SampleDisposable arg = sample;
				TestLogMonitor logMonitor = new TestLogMonitor();

				// ACT
				logMonitor.StartLogging(filterByCurrentThread: true);
				try {
					DisposableUtil.ClearDisposableObject(ref arg);
				} finally {
					logMonitor.StopLogging();
				}

				// ASSERT
				Assert.Equal(null, arg);
				Assert.Equal(1, sample.DisposedCount);
				LogEntry expectedEntry = new LogEntry(null, TraceEventType.Error, $"Fail to dispose the object at 'ClearDisposableObject.Error()': {sample.DisposeErrorMessage}");
				logMonitor.AssertContains(expectedEntry);
			}

			[Fact(DisplayName = "target: null")]
			public void Args_target_null() {
				// ARRANGE
				SampleDisposable arg = null;

				// ACT
				DisposableUtil.ClearDisposableObject(ref arg);

				// ASSERT
				// no ArgumentNullException expected
				Assert.Equal(null, arg);
			}

			#endregion
		}

		#endregion


		#region test - ClearDisposableObjects and ClearDisposableObjectsParallelly

		public abstract class SampleObjectsAgent<Sample, SampleCollection> where Sample : IDisposable where SampleCollection : class, ICollection<Sample> {
			#region overridables

			public abstract bool IsNullValue(Sample item);

			public abstract int GetDisposedCount(Sample item);

			public abstract string GetDisposeErrorMessage(Sample item);

			public abstract Sample[] CreateSampleObjects(params bool[] errorPattern);

			public abstract SampleCollection CreateArgument(Sample[] samples);

			public abstract bool IsCleared(SampleCollection arg);

			#endregion
		}

		public abstract class SampleDisposableObjectsAgent<SampleCollection>: SampleObjectsAgent<SampleDisposable, SampleCollection> where SampleCollection : class, ICollection<SampleDisposable> {
			#region overrides

			public override bool IsNullValue(SampleDisposable item) {
				return item == null;
			}

			public override int GetDisposedCount(SampleDisposable item) {
				// argument checks
				Debug.Assert(item != null);

				return item.DisposedCount;
			}

			public override string GetDisposeErrorMessage(SampleDisposable item) {
				// argument checks
				Debug.Assert(item != null);

				return item.DisposeErrorMessage;
			}

			public override SampleDisposable[] CreateSampleObjects(params bool[] errorPattern) {
				// argument checks
				Debug.Assert(errorPattern != null);

				// create a sample collection
				return (
					from error in errorPattern
					select new SampleDisposable(throwException: error)
				).ToArray();
			}

			#endregion
		}

		public class SampleDisposableListAgent: SampleDisposableObjectsAgent<List<SampleDisposable>> {
			#region data

			public static readonly SampleDisposableListAgent Instance = new SampleDisposableListAgent();

			#endregion


			#region overrides

			public override List<SampleDisposable> CreateArgument(SampleDisposable[] samples) {
				// argument checks
				Debug.Assert(samples != null);

				// create an argument for the target method
				return new List<SampleDisposable>(samples);
			}

			public override bool IsCleared(List<SampleDisposable> arg) {
				// argument checks
				Debug.Assert(arg != null);

				return arg.Count == 0;
			}

			#endregion
		}

		public class SampleDisposableArrayAgent: SampleDisposableObjectsAgent<SampleDisposable[]> {
			#region data

			public static readonly SampleDisposableArrayAgent Instance = new SampleDisposableArrayAgent();

			#endregion


			#region overrides

			public override SampleDisposable[] CreateArgument(SampleDisposable[] samples) {
				// argument checks
				Debug.Assert(samples != null);

				// create an argument for the target method
				return samples.Clone() as SampleDisposable[];
			}

			public override bool IsCleared(SampleDisposable[] arg) {
				// argument checks
				Debug.Assert(arg != null);

				// check whether all items are null
				return arg.All((item) => (item == null));
			}

			#endregion
		}


		public abstract class SampleObjectsClient<Sample, SampleCollection> where Sample : IDisposable where SampleCollection : class, ICollection<Sample> {
			#region data

			protected readonly SampleObjectsAgent<Sample, SampleCollection> SampleObjectsAgent;

			protected readonly bool UseParallel;

			#endregion


			#region properties

			public string TestClassName {
				get {
					return GetType().Name;
				}
			}

			#endregion


			#region creation and disposal

			protected SampleObjectsClient(SampleObjectsAgent<Sample, SampleCollection> sampleObjectsAgent, bool useParallel) {
				// argument checks
				if (sampleObjectsAgent == null) {
					throw new ArgumentNullException(nameof(sampleObjectsAgent));
				}

				// initialize members
				this.SampleObjectsAgent = sampleObjectsAgent;
				this.UseParallel = useParallel;

				return;
			}

			#endregion


			#region methods

			public bool AreAllDisposed(ICollection<Sample> samples) {
				// argument checks
				Debug.Assert(samples != null);

				return samples.All((item) => (IsNullValue(item) || GetDisposedCount(item) == 1));
			}

			protected bool IsNullValue(Sample item) {
				return this.SampleObjectsAgent.IsNullValue(item);
			}

			protected int GetDisposedCount(Sample item) {
				return this.SampleObjectsAgent.GetDisposedCount(item);
			}

			protected string GetDisposeErrorMessage(Sample item) {
				return this.SampleObjectsAgent.GetDisposeErrorMessage(item);
			}

			protected Sample[] CreateSampleObjects(params bool[] errorPattern) {
				return this.SampleObjectsAgent.CreateSampleObjects(errorPattern);
			}

			protected SampleCollection CreateArgument(Sample[] samples) {
				return this.SampleObjectsAgent.CreateArgument(samples);
			}

			protected bool IsCleared(SampleCollection arg) {
				return this.SampleObjectsAgent.IsCleared(arg);
			}

			protected void AssertLog(Sample[] samples, string location, TestLogMonitor actual) {
				// argument checks
				Debug.Assert(samples != null);
				Debug.Assert(location != null);
				Debug.Assert(actual != null);

				// assert
				// Note that the log monitor may store logs which other components report.
				foreach (Sample sample in samples) {
					string errorMessage = GetDisposeErrorMessage(sample);
					if (errorMessage != null) {
						// this sample throws an Exception on Dispose()
						LogEntry expectedEntry = new LogEntry(null, TraceEventType.Error, $"Fail to dispose the object at '{location}': {errorMessage}");
						actual.AssertContains(expectedEntry);
					}
				}

				return;
			}

			#endregion
		}

		public abstract class ClearDisposableObjectsTestBase<Sample, SampleCollection>: SampleObjectsClient<Sample, SampleCollection> where Sample: IDisposable where SampleCollection: class, ICollection<Sample> {
			#region creation and disposal

			protected ClearDisposableObjectsTestBase(SampleObjectsAgent<Sample, SampleCollection> sampleObjectsAgent, bool useParallel): base(sampleObjectsAgent, useParallel) {
			}

			#endregion


			#region overridables

			protected abstract void CallTargetMethod(SampleCollection arg);

			protected abstract void CallTargetMethodAsExtensionMethod(SampleCollection arg);

			#endregion


			#region tests

			[Fact(DisplayName = "general")]
			public void General() {
				// ARRANGE
				Sample[] samples = CreateSampleObjects(false, false, false);
				SampleCollection arg = CreateArgument(samples);

				// ACT
				CallTargetMethod(arg);

				// ASSERT
				Assert.True(AreAllDisposed(samples));
				Assert.True(IsCleared(arg));
			}

			[Fact(DisplayName = "error")]
			public void Error() {
				// ARRANGE
				Sample[] samples = CreateSampleObjects(false, true, true);
				SampleCollection target = CreateArgument(samples);
				TestLogMonitor logMonitor = new TestLogMonitor();

				// ACT
				logMonitor.StartLogging(filterByCurrentThread: !this.UseParallel);
				try {
					CallTargetMethod(target);
				} finally {
					logMonitor.StopLogging();
				}

				// ASSERT
				Assert.True(AreAllDisposed(samples));
				Assert.True(IsCleared(target));
				AssertLog(samples, $"{this.TestClassName}.CallTargetMethod()", logMonitor);
			}

			[Fact(DisplayName = "target: null")]
			public void Args_target_null() {
				// ARRANGE
				SampleCollection arg = null;

				// ACT
				CallTargetMethod(arg);

				// ASSERT
				// no ArgumentNullException expected
			}

			[Fact(DisplayName = "target: empty")]
			public void Args_target_empty() {
				// ARRANGE
				SampleCollection arg = CreateArgument(new Sample[0]);

				// ACT
				CallTargetMethod(arg);

				// ASSERT
				// no ArgumentNullException expected
				Assert.True(IsCleared(arg));
			}

			[Fact(DisplayName = "target: null item")]
			public void Args_target_nullItem() {
				// ARRANGE
				Sample[] samples = CreateSampleObjects(false, false, false, false);
				samples[2] = default(Sample);
				SampleCollection arg = CreateArgument(samples);

				// ACT
				CallTargetMethod(arg);

				// ASSERT
				Assert.True(AreAllDisposed(samples));
				Assert.True(IsCleared(arg));
			}

			[Fact(DisplayName = "general: call as an extension method")]
			public void General_AsExtensionMethod() {
				// ARRANGE
				Sample[] samples = CreateSampleObjects(false, false, false);
				SampleCollection arg = CreateArgument(samples);

				// ACT
				CallTargetMethodAsExtensionMethod(arg);

				// ASSERT
				Assert.True(AreAllDisposed(samples));
				Assert.True(IsCleared(arg));
			}

			#endregion
		}

		public abstract class ClearDisposableObjectsRefTestBase<Sample, SampleCollection>: SampleObjectsClient<Sample, SampleCollection> where Sample : IDisposable where SampleCollection : class, ICollection<Sample> {
			#region creation and disposal

			protected ClearDisposableObjectsRefTestBase(SampleObjectsAgent<Sample, SampleCollection> sampleObjectsAgent, bool useParallel) : base(sampleObjectsAgent, useParallel) {
			}

			#endregion


			#region overridables

			protected abstract void CallTargetMethod(ref SampleCollection target);

			#endregion


			#region tests

			[Fact(DisplayName = "general")]
			public void General() {
				// ARRANGE
				Sample[] samples = CreateSampleObjects(false, false, false);
				SampleCollection arg = CreateArgument(samples);
				SampleCollection backup = arg;

				// ACT
				CallTargetMethod(ref arg);

				// ASSERT
				Assert.Equal(null, arg);
				Assert.True(AreAllDisposed(samples));
				Assert.True(IsCleared(backup));
			}

			[Fact(DisplayName = "error")]
			public void Error() {
				// ARRANGE
				Sample[] samples = CreateSampleObjects(false, true, true);
				SampleCollection arg = CreateArgument(samples);
				SampleCollection backup = arg;
				TestLogMonitor logMonitor = new TestLogMonitor();

				// ACT
				logMonitor.StartLogging(filterByCurrentThread: !this.UseParallel);
				try {
					CallTargetMethod(ref arg);
				} finally {
					logMonitor.StopLogging();
				}

				// ASSERT
				Assert.Equal(null, arg);
				Assert.True(AreAllDisposed(samples));
				Assert.True(IsCleared(backup));
				AssertLog(samples, $"{this.TestClassName}.CallTargetMethod()", logMonitor);
			}

			[Fact(DisplayName = "target: null")]
			public void Args_target_null() {
				// ARRANGE
				SampleCollection arg = null;

				// ACT
				CallTargetMethod(ref arg);

				// ASSERT
				// no ArgumentNullException expected
				Assert.Equal(null, arg);
			}

			[Fact(DisplayName = "target: empty")]
			public void Args_target_empty() {
				// ARRANGE
				SampleCollection arg = CreateArgument(new Sample[0]);
				SampleCollection backup = arg;

				// ACT
				CallTargetMethod(ref arg);

				// ASSERT
				// expected no ArgumentNullException
				Assert.Equal(null, arg);
				Assert.True(IsCleared(backup));
			}

			[Fact(DisplayName = "target: null item")]
			public void Args_target_nullItem() {
				// ARRANGE
				Sample[] samples = CreateSampleObjects(false, false, false, false);
				samples[1] = default(Sample);
				SampleCollection arg = CreateArgument(samples);
				SampleCollection backup = arg;

				// ACT
				CallTargetMethod(ref arg);

				// ASSERT
				Assert.Equal(null, arg);
				Assert.True(AreAllDisposed(samples));
				Assert.True(IsCleared(backup));
			}

			#endregion
		}


		public class ClearDisposableObjects_Collection: ClearDisposableObjectsTestBase<SampleDisposable, List<SampleDisposable>> {
			#region creation and disposal

			public ClearDisposableObjects_Collection(): base(SampleDisposableListAgent.Instance, useParallel: false) {
			}

			#endregion


			#region overridables

			protected override void CallTargetMethod(List<SampleDisposable> target) {
				// argument checks
				// target can be null

				// call the test target
				DisposableUtil.ClearDisposableObjects(target);
			}

			protected override void CallTargetMethodAsExtensionMethod(List<SampleDisposable> target) {
				// argument checks
				// target can be null

				// call the test target in extension method form
				target.ClearDisposableObjects();
			}

			#endregion
		}

		public class ClearDisposableObjects_CollectionRef: ClearDisposableObjectsRefTestBase<SampleDisposable, List<SampleDisposable>> {
			#region creation and disposal

			public ClearDisposableObjects_CollectionRef(): base(SampleDisposableListAgent.Instance, useParallel: false) {
			}

			#endregion


			#region overridables

			protected override void CallTargetMethod(ref List<SampleDisposable> target) {
				// argument checks
				// target can be null

				// call the test target
				DisposableUtil.ClearDisposableObjects<SampleDisposable, List<SampleDisposable>>(ref target);
			}

			#endregion
		}

		public class ClearDisposableObjects_Array: ClearDisposableObjectsTestBase<SampleDisposable, SampleDisposable[]> {
			#region creation and disposal

			public ClearDisposableObjects_Array(): base(SampleDisposableArrayAgent.Instance, useParallel: false) {
			}

			#endregion


			#region overridables

			protected override void CallTargetMethod(SampleDisposable[] target) {
				// argument checks
				// target can be null

				// call the test target
				DisposableUtil.ClearDisposableObjects(target);
			}

			protected override void CallTargetMethodAsExtensionMethod(SampleDisposable[] target) {
				// argument checks
				// target can be null

				// call the test target in extension method form
				target.ClearDisposableObjects();
			}

			#endregion
		}

		public class ClearDisposableObjects_ArrayRef: ClearDisposableObjectsRefTestBase<SampleDisposable, SampleDisposable[]> {
			#region creation and disposal

			public ClearDisposableObjects_ArrayRef() : base(SampleDisposableArrayAgent.Instance, useParallel: false) {
			}

			#endregion


			#region overridables

			protected override void CallTargetMethod(ref SampleDisposable[] target) {
				// argument checks
				// target can be null

				// call the test target
				DisposableUtil.ClearDisposableObjects(ref target);
			}

			#endregion
		}

		public class ClearDisposableObjectsParallelly_Collection: ClearDisposableObjectsTestBase<SampleDisposable, List<SampleDisposable>> {
			#region creation and disposal

			public ClearDisposableObjectsParallelly_Collection() : base(SampleDisposableListAgent.Instance, useParallel: true) {
			}

			#endregion


			#region overridables

			protected override void CallTargetMethod(List<SampleDisposable> target) {
				// argument checks
				// target can be null

				// call the test target
				DisposableUtil.ClearDisposableObjectsParallelly(target);
			}

			protected override void CallTargetMethodAsExtensionMethod(List<SampleDisposable> target) {
				// argument checks
				// target can be null

				// call the test target in extension method form
				target.ClearDisposableObjectsParallelly();
			}

			#endregion
		}

		public class ClearDisposableObjectsParallelly_CollectionRef: ClearDisposableObjectsRefTestBase<SampleDisposable, List<SampleDisposable>> {
			#region creation and disposal

			public ClearDisposableObjectsParallelly_CollectionRef() : base(SampleDisposableListAgent.Instance, useParallel: true) {
			}

			#endregion


			#region overridables

			protected override void CallTargetMethod(ref List<SampleDisposable> target) {
				// argument checks
				// target can be null

				// call the test target
				DisposableUtil.ClearDisposableObjectsParallelly<SampleDisposable, List<SampleDisposable>>(ref target);
			}

			#endregion
		}

		public class ClearDisposableObjectsParallelly_Array: ClearDisposableObjectsTestBase<SampleDisposable, SampleDisposable[]> {
			#region creation and disposal

			public ClearDisposableObjectsParallelly_Array() : base(SampleDisposableArrayAgent.Instance, useParallel: true) {
			}

			#endregion


			#region overridables

			protected override void CallTargetMethod(SampleDisposable[] target) {
				// argument checks
				// target can be null

				// call the test target
				DisposableUtil.ClearDisposableObjectsParallelly(target);
			}

			protected override void CallTargetMethodAsExtensionMethod(SampleDisposable[] target) {
				// argument checks
				// target can be null

				// call the test target in extension method form
				target.ClearDisposableObjectsParallelly();
			}

			#endregion
		}

		public class ClearDisposableObjectsParallelly_ArrayRef: ClearDisposableObjectsRefTestBase<SampleDisposable, SampleDisposable[]> {
			#region creation and disposal

			public ClearDisposableObjectsParallelly_ArrayRef() : base(SampleDisposableArrayAgent.Instance, useParallel: true) {
			}

			#endregion


			#region overridables

			protected override void CallTargetMethod(ref SampleDisposable[] target) {
				// argument checks
				// target can be null

				// call the test target
				DisposableUtil.ClearDisposableObjectsParallelly(ref target);
			}

			#endregion
		}

		#endregion
	}
}
