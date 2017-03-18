using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;


namespace MAPE.Utils.Test {
	public class ObjectDataTestBase {
		#region types

		public abstract class ValueSample {
			#region data

			public readonly string Name;

			public readonly ObjectDataValueType Type;

			#endregion


			#region creation and disposal

			public ValueSample(string name, ObjectDataValueType type) {
				// argument checks
				if (string.IsNullOrEmpty(name)) {
					throw new ArgumentNullException(nameof(name));
				}
				switch (type) {
					case ObjectDataValueType.Integer:
					case ObjectDataValueType.Float:
					case ObjectDataValueType.Boolean:
					case ObjectDataValueType.String:
					case ObjectDataValueType.Object:
					case ObjectDataValueType.Array:
					case ObjectDataValueType.Null:
						break;
					default:
						// includes ObjectDataValueType.Unknown
						throw new ArgumentOutOfRangeException(nameof(type));
				}

				// initialize members
				this.Name = name;
				this.Type = type;
			}

			#endregion


			#region methods

			protected static ObjectDataValueType AdjustType(ObjectDataValueType type, object rawValue) {
				return (rawValue == null) ? ObjectDataValueType.Null : type;
			}

			public static IObjectDataValue CreateValue(IObjectData data, object rawValue) {
				if (rawValue == null) {
					return ObjectValueSample.CreateValue(data, (IDictionary<string, object>)null);
				} else if (rawValue is int) {
					return Int32ValueSample.CreateValue(data, (int)rawValue);
				} else if (rawValue is long) {
					return Int64ValueSample.CreateValue(data, (long)rawValue);
				} else if (rawValue is double) {
					return DoubleValueSample.CreateValue(data, (double)rawValue);
				} else if (rawValue is bool) {
					return BooleanValueSample.CreateValue(data, (bool)rawValue);
				} else if (rawValue is string) {
					return StringValueSample.CreateValue(data, (string)rawValue);
				} else if (rawValue is IDictionary<string, object>) {
					return ObjectValueSample.CreateValue(data, (IDictionary<string, object>)rawValue);
				} else if (rawValue is IEnumerable<object>) {
					return ArrayValueSample.CreateValue(data, (IEnumerable<object>)rawValue);
				} else {
					throw new ArgumentException($"Its type '{rawValue.GetType().FullName}' is unexpected.", nameof(rawValue));
				}
			}

			public static void AssertEqual(object expected, IObjectDataValue actual) {
				// argument checks
				if (actual == null) {
					throw new ArgumentNullException(nameof(actual));
				}
				ObjectDataValueType actualType = actual.Type;

				// The code will be a bit simple if the cases are swithed by actualType,
				// but I switch the cases by type of expected,
				// because this way give more reasonable error message on error.
				//   ex. the case expected is 3 and actualType is String causes:
				//         switching by expected:   assertion error
				//         switching by actualType: InvalidCastException
				if (expected == null) {
					switch (actualType) {
						case ObjectDataValueType.Null:
							break;
						case ObjectDataValueType.String:
							StringValueSample.AssertEqual((string)null, actual);
							break;
						case ObjectDataValueType.Object:
							ObjectValueSample.AssertEqual((IDictionary<string, object>)null, actual);
							break;
						case ObjectDataValueType.Array:
							ArrayValueSample.AssertEqual((IEnumerable<object>)null, actual);
							break;
						default:
							Assert.True(false, $"The value of IObjectDataValue.Type is unexpected: '{actualType}'");
							break;
					}
				} else if (expected is int) {
					Assert.Equal(ObjectDataValueType.Integer, actualType);
					Int32ValueSample.AssertEqual((int)expected, actual);
				} else if (expected is long) {
					Assert.Equal(ObjectDataValueType.Integer, actualType);
					Int64ValueSample.AssertEqual((long)expected, actual);
				} else if (expected is double) {
					Assert.Equal(ObjectDataValueType.Float, actualType);
					DoubleValueSample.AssertEqual((double)expected, actual);
				} else if (expected is bool) {
					Assert.Equal(ObjectDataValueType.Boolean, actualType);
					BooleanValueSample.AssertEqual((bool)expected, actual);
				} else if (expected is string) {
					Assert.Equal(ObjectDataValueType.String, actualType);
					StringValueSample.AssertEqual((string)expected, actual);
				} else if (expected is IDictionary<string, object>) {
					Assert.Equal(ObjectDataValueType.Object, actualType);
					ObjectValueSample.AssertEqual((IDictionary<string, object>)expected, actual);
				} else if (expected is IEnumerable<object>) {
					Assert.Equal(ObjectDataValueType.Array, actualType);
					ArrayValueSample.AssertEqual((IEnumerable<object>)expected, actual);
				} else {
					Assert.True(false, $"The type of expected is unexpected: '{expected.GetType().FullName}'");
				}
			}

			#endregion


			#region overridables

			public abstract object GetRawValue();

			public abstract IObjectDataValue CreateValue(IObjectData data);

			public abstract void AssertEquals(IObjectDataValue value);

			#endregion
		}

		public abstract class ValueSample<T>: ValueSample {
			#region data

			public readonly T RawValue;

			#endregion


			#region creation and disposal

			public ValueSample(string name, ObjectDataValueType type, T rawValue): base(name, type) {
				// initialize members
				this.RawValue = rawValue;
			}

			#endregion


			#region overrides

			public override object GetRawValue() {
				return this.RawValue;
			}

			#endregion
		}

		public class Int32ValueSample: ValueSample<int> {
			#region creation and disposal

			public Int32ValueSample(string name, int rawValue): base(name, ObjectDataValueType.Integer, rawValue) {
			}

			#endregion


			#region methods

			public static IObjectDataValue CreateValue(IObjectData data, int rawValue) {
				// argument checks
				if (data == null) {
					throw new ArgumentNullException(nameof(data));
				}

				return data.CreateValue(rawValue);
			}

			public static void AssertEqual(int expected, IObjectDataValue actual) {
				// argument checks
				if (actual == null) {
					throw new ArgumentNullException(nameof(actual));
				}

				Assert.Equal(expected, actual.ExtractInt32Value());
			}

			#endregion


			#region overrides

			public override IObjectDataValue CreateValue(IObjectData data) {
				return CreateValue(data, this.RawValue);
			}

			public override void AssertEquals(IObjectDataValue actual) {
				AssertEqual(this.RawValue, actual);
			}

			#endregion
		}

		public class Int64ValueSample: ValueSample<long> {
			#region creation and disposal

			public Int64ValueSample(string name, long rawValue) : base(name, ObjectDataValueType.Integer, rawValue) {
			}

			#endregion


			#region methods

			public static IObjectDataValue CreateValue(IObjectData data, long rawValue) {
				// argument checks
				if (data == null) {
					throw new ArgumentNullException(nameof(data));
				}

				return data.CreateValue(rawValue);
			}

			public static void AssertEqual(long expected, IObjectDataValue actual) {
				// argument checks
				if (actual == null) {
					throw new ArgumentNullException(nameof(actual));
				}

				Assert.Equal(expected, actual.ExtractInt64Value());
			}

			#endregion


			#region overrides

			public override IObjectDataValue CreateValue(IObjectData data) {
				return CreateValue(data, this.RawValue);
			}

			public override void AssertEquals(IObjectDataValue actual) {
				AssertEqual(this.RawValue, actual);
			}

			#endregion
		}

		public class DoubleValueSample: ValueSample<double> {
			#region creation and disposal

			public DoubleValueSample(string name, double rawValue) : base(name, ObjectDataValueType.Float, rawValue) {
			}

			#endregion


			#region methods

			public static IObjectDataValue CreateValue(IObjectData data, double rawValue) {
				// argument checks
				if (data == null) {
					throw new ArgumentNullException(nameof(data));
				}

				return data.CreateValue(rawValue);
			}

			public static void AssertEqual(double expected, IObjectDataValue actual) {
				// argument checks
				if (actual == null) {
					throw new ArgumentNullException(nameof(actual));
				}

				Assert.Equal(expected, actual.ExtractDoubleValue());
			}

			#endregion


			#region overrides

			public override IObjectDataValue CreateValue(IObjectData data) {
				return CreateValue(data, this.RawValue);
			}

			public override void AssertEquals(IObjectDataValue actual) {
				AssertEqual(this.RawValue, actual);
			}

			#endregion
		}

		public class BooleanValueSample: ValueSample<bool> {
			#region creation and disposal

			public BooleanValueSample(string name, bool rawValue) : base(name, ObjectDataValueType.Boolean, rawValue) {
			}

			#endregion


			#region methods

			public static IObjectDataValue CreateValue(IObjectData data, bool rawValue) {
				// argument checks
				if (data == null) {
					throw new ArgumentNullException(nameof(data));
				}

				return data.CreateValue(rawValue);
			}

			public static void AssertEqual(bool expected, IObjectDataValue actual) {
				// argument checks
				if (actual == null) {
					throw new ArgumentNullException(nameof(actual));
				}

				Assert.Equal(expected, actual.ExtractBooleanValue());
			}

			#endregion


			#region overrides

			public override IObjectDataValue CreateValue(IObjectData data) {
				return CreateValue(data, this.RawValue);
			}

			public override void AssertEquals(IObjectDataValue actual) {
				AssertEqual(this.RawValue, actual);
			}

			#endregion
		}

		public class StringValueSample: ValueSample<string> {
			#region creation and disposal

			public StringValueSample(string name, string rawValue) : base(name, ObjectDataValueType.String, rawValue) {
			}

			#endregion


			#region methods

			public static IObjectDataValue CreateValue(IObjectData data, string rawValue) {
				// argument checks
				if (data == null) {
					throw new ArgumentNullException(nameof(data));
				}

				return data.CreateValue(rawValue);
			}

			public static void AssertEqual(string expected, IObjectDataValue actual) {
				// argument checks
				if (actual == null) {
					throw new ArgumentNullException(nameof(actual));
				}

				Assert.Equal(expected, actual.ExtractStringValue());
			}

			#endregion


			#region overrides

			public override IObjectDataValue CreateValue(IObjectData data) {
				return CreateValue(data, this.RawValue);
			}

			public override void AssertEquals(IObjectDataValue actual) {
				AssertEqual(this.RawValue, actual);
			}

			#endregion
		}

		public class ObjectValueSample: ValueSample<IDictionary<string, object>> {
			#region creation and disposal

			public ObjectValueSample(string name, IDictionary<string, object> rawValue) : base(name, AdjustType(ObjectDataValueType.Object, rawValue), rawValue) {
			}

			#endregion


			#region methods

			public static IObjectDataValue CreateValue(IObjectData data, IDictionary<string, object> rawValue) {
				// argument checks
				if (data == null) {
					throw new ArgumentNullException(nameof(data));
				}

				// build an object
				IObjectData obj = null;
				if (rawValue != null) {
					obj = data.CreateObject();
					foreach (var prop in rawValue) {
						obj.SetValue(prop.Key, CreateValue(data, prop.Value));
					}
				}

				// create a value for the object
				return data.CreateValue(obj);
			}

			public static void AssertEqual(IDictionary<string, object> expected, IObjectDataValue actual) {
				// argument checks
				if (actual == null) {
					throw new ArgumentNullException(nameof(actual));
				}
				IObjectData actualValue = actual.ExtractObjectValue();

				// exclude null case
				if (expected == null || actual == null) {
					// assert whether both expected and actualValue are null
					Assert.Equal((object)expected, (object)actualValue);
					return;
				}

				// property count
				Assert.Equal(expected.Count, actualValue.GetNames().Count());

				// each property value
				foreach (var pair in expected) {
					AssertEqual(pair.Value, actualValue.GetValue(pair.Key));
				}
			}

			#endregion


			#region overrides

			public override IObjectDataValue CreateValue(IObjectData data) {
				return CreateValue(data, this.RawValue);
			}

			public override void AssertEquals(IObjectDataValue actual) {
				AssertEqual(this.RawValue, actual);
			}

			#endregion
		}

		public class ArrayValueSample: ValueSample<IEnumerable<object>> {
			#region creation and disposal

			public ArrayValueSample(string name, IEnumerable<object> rawValue) : base(name, AdjustType(ObjectDataValueType.Array, rawValue), rawValue) {
			}

			#endregion


			#region methods

			public static IObjectDataValue CreateValue(IObjectData data, IEnumerable<object> rawValue) {
				// argument checks
				if (data == null) {
					throw new ArgumentNullException(nameof(data));
				}

				// build an array
				IEnumerable<IObjectDataValue> values = null;
				if (rawValue != null) {
					values = rawValue.Select(o => CreateValue(data, o)).ToArray();
				}

				// create a value for the array
				return data.CreateValue(values);
			}

			public static void AssertEqual(IEnumerable<object> expected, IObjectDataValue actual) {
				// argument checks
				if (actual == null) {
					throw new ArgumentNullException(nameof(actual));
				}
				IEnumerable<IObjectDataValue> actualValue = actual.ExtractArrayValue();

				// exclude null case
				if (expected == null || actual == null) {
					// assert whether both expected and actualValue are null
					Assert.Equal((object)expected, (object)actualValue);
					return;
				}

				// assert each value in the array
				foreach (var assertionCase in expected.Zip(actualValue, (e, a) => new Tuple<object, IObjectDataValue>(e, a))) {
					AssertEqual(assertionCase.Item1, assertionCase.Item2);
				}
			}

			#endregion


			#region overrides

			public override IObjectDataValue CreateValue(IObjectData data) {
				return CreateValue(data, this.RawValue);
			}

			public override void AssertEquals(IObjectDataValue actual) {
				AssertEqual(this.RawValue, actual);
			}

			#endregion
		}

		#endregion


		#region data

		public static readonly Int32ValueSample Int32MinValueSample = new Int32ValueSample("Int32MinValue", int.MinValue);

		public static readonly Int32ValueSample Int32MaxValueSample = new Int32ValueSample("Int32MaxValue", int.MaxValue);

		public static readonly Int64ValueSample Int64MinValueSample = new Int64ValueSample("Int64MinValue", long.MinValue);

		public static readonly Int64ValueSample Int64MaxValueSample = new Int64ValueSample("Int64MaxValue", long.MaxValue);

		public static readonly DoubleValueSample DoubleMinValueSample = new DoubleValueSample("DoubleMinValue", double.MinValue);

		public static readonly DoubleValueSample DoubleMaxValueSample = new DoubleValueSample("DoubleMaxValue", double.MaxValue);

		public static readonly DoubleValueSample DoubleEpsilonValueSample = new DoubleValueSample("DoubleEpsilonValue", double.Epsilon);

		public static readonly BooleanValueSample BooleanFalseValueSample = new BooleanValueSample("BooleanFalseValue", false);

		public static readonly BooleanValueSample BooleanTrueValueSample = new BooleanValueSample("BooleanTrueValue", true);

		public static readonly StringValueSample StringGeneralValueSample = new StringValueSample("StringGeneralValue", "ABC");

		public static readonly StringValueSample StringNullValueSample = new StringValueSample("StringNullValue", null);

		public static readonly StringValueSample StringEmptyValueSample = new StringValueSample("StringEmptyValue", string.Empty);

		public static readonly StringValueSample StringMultiLinesValueSample = new StringValueSample("StringMultiLinesValue", "ABC\nxyz\n123");

		public static readonly ValueSample[] SimpleValueSamples = new ValueSample[] {
			Int32MinValueSample,
			Int32MaxValueSample,
			Int64MinValueSample,
			Int64MaxValueSample,
			DoubleMinValueSample,
			DoubleMaxValueSample,
			DoubleEpsilonValueSample,
			BooleanFalseValueSample,
			BooleanTrueValueSample,
			StringGeneralValueSample,
			StringNullValueSample,
			StringEmptyValueSample,
			StringMultiLinesValueSample
		};

		public static readonly ObjectValueSample ObjectNullValueSample = new ObjectValueSample("ObjectNullValue", null);

		public static readonly ObjectValueSample ObjectFlatValueSample;

		public static readonly ObjectValueSample ObjectNestedValueSample;

		public static readonly ArrayValueSample ArrayNullValueSample = new ArrayValueSample("ArrayNullValue", null);

		public static readonly ArrayValueSample ArrayFlatValueSample;

		public static readonly ArrayValueSample ArrayNestedValueSample;

		public static readonly ValueSample[] AllValueSamples;

		#endregion


		#region creation and disposal

		static ObjectDataTestBase() {
			// ObjectFlatValueSample
			IDictionary<string, object> dictionary = SimpleValueSamples.ToDictionary(s => s.Name, s => s.GetRawValue());
			ObjectFlatValueSample = new ObjectValueSample("ObjectFlatValue", dictionary);

			// ArrayFlatValueSample
			IEnumerable<object> array = SimpleValueSamples.Select(s => s.GetRawValue()).ToArray();
			ArrayFlatValueSample = new ArrayValueSample("ArrayFlatValue", array);

			// ObjectNestedValueSample
			dictionary = SimpleValueSamples.ToDictionary(s => s.Name, s => s.GetRawValue());
			dictionary.Add(ObjectFlatValueSample.Name, ObjectFlatValueSample.GetRawValue());
			dictionary.Add(ArrayFlatValueSample.Name, ArrayFlatValueSample.GetRawValue());
			ObjectNestedValueSample = new ObjectValueSample("ObjectNestedValue", dictionary);

			// ArrayFlatValueSample
			List<object> list = new List<object>(array);
			list.Add(ObjectFlatValueSample.GetRawValue());
			list.Add(ArrayFlatValueSample.GetRawValue());
			ArrayNestedValueSample = new ArrayValueSample("ArrayNestedValue", list);

			// AllValueSamples
			List<ValueSample> buf = new List<ValueSample>(SimpleValueSamples);
			buf.Add(ObjectFlatValueSample);
			buf.Add(ObjectNestedValueSample);
			buf.Add(ArrayFlatValueSample);
			buf.Add(ArrayNestedValueSample);
			AllValueSamples = buf.ToArray();
		}

		#endregion


		#region test bases

		public abstract class TestBase {
			#region data

			protected readonly Func<IObjectData> createEmptyObjectData;

			#endregion


			#region creation and disposal

			protected TestBase(Func<IObjectData> createEmptyObjectData) {
				// argument checks
				if (createEmptyObjectData == null) {
					throw new ArgumentNullException(nameof(createEmptyObjectData));
				}

				// initialize members
				this.createEmptyObjectData = createEmptyObjectData;
			}

			#endregion


			#region methods

			protected IObjectData CreateEmptyObjectData() {
				// state checks
				Debug.Assert(this.createEmptyObjectData != null);

				return this.createEmptyObjectData();
			}

			#endregion
		}

		// Test for the protocol of IObjectDataTest family
		public abstract class BasicProtocolTestBase: TestBase {
			#region creation and disposal

			protected BasicProtocolTestBase(Func<IObjectData> createEmptyObjectData): base(createEmptyObjectData) {
			}

			#endregion


			#region tests - empty

			[Fact(DisplayName = "Empty")]
			public void Empty() {
				// ACT
				IObjectData data = CreateEmptyObjectData();

				// ASSERT
				Assert.Equal(0, data.GetNames().Count());
			}

			#endregion


			#region tests - IObjectDataValue

			[Fact(DisplayName = "Value, Integer as Int32, min value")]
			public void Int32_MinValue() {
				TestValue(Int32MinValueSample);
			}

			[Fact(DisplayName = "Value, Integer as Int32, max value")]
			public void Int32_MaxValue() {
				TestValue(Int32MaxValueSample);
			}

			[Fact(DisplayName = "Value, Integer as Int64, min value")]
			public void Int64_MinValue() {
				TestValue(Int64MinValueSample);
			}

			[Fact(DisplayName = "Value, Integer as Int64, max value")]
			public void Int64_MaxValue() {
				TestValue(Int64MaxValueSample);
			}

			[Fact(DisplayName = "Value, Float, min value")]
			public void Double_MinValue() {
				TestValue(DoubleMinValueSample);
			}

			[Fact(DisplayName = "Value, Float, max value")]
			public void Double_MaxValue() {
				TestValue(DoubleMaxValueSample);
			}

			[Fact(DisplayName = "Value, Float, epsilon value")]
			public void Double_EpsilonValue() {
				TestValue(DoubleEpsilonValueSample);
			}

			[Fact(DisplayName = "Value, Boolean, false")]
			public void Boolean_False() {
				TestValue(BooleanFalseValueSample);
			}

			[Fact(DisplayName = "Value, Boolean, true")]
			public void Boolean_True() {
				TestValue(BooleanTrueValueSample);
			}

			[Fact(DisplayName = "Value, String, general")]
			public void String_General() {
				TestValue(StringGeneralValueSample);
			}

			[Fact(DisplayName = "Value, String, null")]
			public void String_Null() {
				TestValue(StringNullValueSample);
			}

			[Fact(DisplayName = "Value, String, empty")]
			public void String_Empty() {
				TestValue(StringEmptyValueSample);
			}

			[Fact(DisplayName = "Value, String, multi lines")]
			public void String_MultiLines() {
				TestValue(StringMultiLinesValueSample);
			}

			[Fact(DisplayName = "Value, Object, null")]
			public void Object_Null() {
				TestValue(ObjectNullValueSample);
			}

			[Fact(DisplayName = "Value, Object, flat")]
			public void Object_Flat() {
				TestValue(ObjectFlatValueSample);
			}

			[Fact(DisplayName = "Value, Object, nested")]
			public void Object_Nested() {
				TestValue(ObjectNestedValueSample);
			}

			[Fact(DisplayName = "Value, Array, null")]
			public void Array_Null() {
				TestValue(ArrayNullValueSample);
			}

			[Fact(DisplayName = "Value, Array, flat")]
			public void Array_Flat() {
				TestValue(ArrayFlatValueSample);
			}

			[Fact(DisplayName = "Value, Array, nested")]
			public void Array_Nested() {
				TestValue(ArrayNestedValueSample);
			}

			#endregion


			#region tests - CreateObject()

			[Fact(DisplayName = "CreateObject()")]
			public void CreateObject() {
				// ARRANGE
				IObjectData data = CreateEmptyObjectData();

				// ACT
				IObjectData obj = data.CreateObject();

				// ASSERT
				Assert.Equal(0, data.GetNames().Count());
			}

			#endregion


			#region tests - SetValue() and GetValue()

			[Fact(DisplayName = "SetValue() and GetValue(), general")]
			public void SetValueAndGetValue_General() {
				// ARRANGE
				IEnumerable<ValueSample> samples = AllValueSamples;
				IObjectData data = CreateEmptyObjectData();

				// ACT
				foreach (ValueSample sample in samples) {
					data.SetValue(sample.Name, sample.CreateValue(data));
				}
				IObjectDataValue[] actualValues = samples.Select(s => data.GetValue(s.Name)).ToArray();

				// ASSERT

				// names
				IEnumerable<string> expectedNames = samples.Select(s => s.Name);
				Assert.Equal(expectedNames, data.GetNames());

				// values
				IEnumerable<Tuple<ValueSample, IObjectDataValue>> assertionCases = samples.Zip(actualValues, (s, v) => new Tuple<ValueSample, IObjectDataValue>(s, v));
				foreach (var assertionCase in assertionCases) {
					assertionCase.Item1.AssertEquals(assertionCase.Item2);
				}
			}

			[Fact(DisplayName = "SetValue(), null value")]
			public void SetValue_Null() {
				// ARRANGE
				IObjectData data = CreateEmptyObjectData();
				data.SetValue("Value1", data.CreateValue(1));
				data.SetValue("Value2", data.CreateValue(2));

				// ACT
				data.SetValue("Value1", null);
				data.SetValue("Value3", null);

				// ASSERT

				// name
				Assert.Equal(1, data.GetNames().Count());
				Assert.Equal("Value2", data.GetNames().FirstOrDefault());

				// value
				Assert.Equal(2, data.GetValue("Value2").ExtractInt32Value());
			}

			#endregion


			#region tests - RemoveValue()

			[Fact(DisplayName = "RemoveValue()")]
			public void RemoveValue() {
				// ARRANGE
				IObjectData data = CreateEmptyObjectData();
				data.SetValue("Value1", data.CreateValue(1));
				data.SetValue("Value2", data.CreateValue(2));

				// ACT
				data.RemoveValue("Value1");
				data.RemoveValue("Value3");

				// ASSERT

				// name
				Assert.Equal(1, data.GetNames().Count());
				Assert.Equal("Value2", data.GetNames().FirstOrDefault());

				// value
				Assert.Equal(2, data.GetValue("Value2").ExtractInt32Value());
			}

			#endregion


			// ToDo: Equals test

			#region methods

			protected void TestValue(ValueSample sample) {
				// argument checks
				Debug.Assert(sample != null);

				// ARRANGE
				IObjectData data = CreateEmptyObjectData();

				// ACT
				// create IObjectDataValue by data.CreateValue(X)
				IObjectDataValue value = sample.CreateValue(data);

				// ASSERT
				Assert.Equal(sample.Type, value.Type);
				// assert that the value.GetXValue() returns the raw sample value
				sample.AssertEquals(value);
			}

			#endregion
		}


#if N
		public abstract class TestBase {
		#region data

			protected readonly Func<IObjectData> createEmptyObjectData;

		#endregion


		#region creation and disposal

			protected TestBase(Func<IObjectData> createEmptyObjectData) {
				// argument checks
				if (createEmptyObjectData == null) {
					throw new ArgumentNullException(nameof(createEmptyObjectData));
				}

				// initialize members
				this.createEmptyObjectData = createEmptyObjectData;
			}

		#endregion


		#region tests

			[Fact(DisplayName = "Defaultable, Basic")]
			public void Defaultable_Basic() {
				// ARRANGE
				string name = "Value";
				T defaultValue = GetSampleValue();
				T value = GetSampleValue();
				Debug.Assert(AreEqualValues(value, defaultValue) == false);

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, value);
				T actual = GetValueAdapter(data, name, defaultValue);

				// ASSERT
				// Note that AreEqualValues() should be used to compare values
				Assert.Equal(true, AreEqualValues(value, actual));
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Defaultable, Overriding")]
			public void Defaultable_Overriding() {
				// ARRANGE
				string name = "Value";
				T originalValue = GetSampleValue();
				T defaultValue = originalValue;
				T value = GetSampleValue();
				Debug.Assert(AreEqualValues(value, defaultValue) == false);
				Debug.Assert(AreEqualValues(value, originalValue) == false);

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, originalValue);
				SetValueAdapter(data, name, value);
				T actual = GetValueAdapter(data, name, defaultValue);

				// ASSERT
				// Note that AreEqualValues() should be used to compare values
				Assert.Equal(true, AreEqualValues(value, actual));
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Defaultable, Overriding by omission")]
			public void Defaultable_OverridingByOmission() {
				// ARRANGE
				string name = "Value";
				T defaultValue = GetSampleValue();
				T originalValue = GetSampleValue();
				T value = defaultValue;
				Debug.Assert(AreEqualValues(value, defaultValue));
				Debug.Assert(AreEqualValues(value, originalValue) == false);

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, originalValue);
				SetValueAdapter(data, name, value, omitDefault: true, isDefault: true); // omission
				T actual = GetValueAdapter(data, name, defaultValue);

				// ASSERT
				// Note that AreEqualValues() should be used to compare values
				Assert.Equal(true, AreEqualValues(value, actual));
				Assert.Equal(0, data.Names.Count());
				Assert.Equal(null, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Defaultable, omitDefault: false, isDefault: true")]
			public void Defaultable_Args_omitDefault_false_isDefault_true() {
				// ARRANGE
				string name = "Value";
				T defaultValue = GetSampleValue();
				T value = GetSampleValue();
				Debug.Assert(AreEqualValues(value, defaultValue) == false);

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, value, omitDefault: false, isDefault: true);
				T actual = GetValueAdapter(data, name, defaultValue);

				// ASSERT
				// Note that AreEqualValues() should be used to compare values
				Assert.Equal(true, AreEqualValues(value, actual));
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Defaultable, omitDefault: true, isDefault: false")]
			public void Defaultable_Args_omitDefault_true_isDefault_false() {
				// ARRANGE
				string name = "Value";
				T defaultValue = GetSampleValue();
				T value = GetSampleValue();
				Debug.Assert(AreEqualValues(value, defaultValue) == false);

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, value, omitDefault: true, isDefault: false);
				T actual = GetValueAdapter(data, name, defaultValue);

				// ASSERT
				// Note that AreEqualValues() should be used to compare values
				Assert.Equal(true, AreEqualValues(value, actual));
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Defaultable, omitDefault: true, isDefault: true")]
			public void Defaultable_Args_omitDefault_true_isDefault_true() {
				// ARRANGE
				string name = "Value";
				T defaultValue = GetSampleValue();
				T value = defaultValue;
				Debug.Assert(AreEqualValues(value, defaultValue));

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, value, omitDefault: true, isDefault: true);
				T actual = GetValueAdapter(data, name, defaultValue);

				// ASSERT
				// Note that AreEqualValues() should be used to compare values
				Assert.Equal(true, AreEqualValues(value, actual));
				Assert.Equal(0, data.Names.Count());
				Assert.Equal(null, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Defaultable, name: null")]
			public void Defaultable_Args_name_null() {
				IObjectData data = CreateEmptyObjectData();
				T defaultValue = GetSampleValue();
				T value = GetSampleValue();

				Assert.Throws<ArgumentNullException>(
					() => {
						GetValueAdapter(data, null, defaultValue);
					}
				);
				Assert.Throws<ArgumentNullException>(
					() => {
						SetValueAdapter(data, null, value);
					}
				);
			}

		#endregion


		#region methods

			protected IObjectData CreateEmptyObjectData() {
				// state checks
				Debug.Assert(this.createEmptyObjectData != null);

				return this.createEmptyObjectData();
			}

			protected bool HasEqualValues(T[] sampleValues) {
				// argument checks
				Debug.Assert(sampleValues != null);

				// check whether there are items which have the equal value.
				for (int i = 0; i < sampleValues.Length; ++i) {
					T t = sampleValues[i];
					for (int j = i + 1; j < sampleValues.Length; ++j) {
						if (AreEqualValues(t, sampleValues[j])) {
							return true;
						}
					}
				}

				return false;
			}

			protected T GetSampleValue() {
				// state checks
				Debug.Assert(this.sampleValues != null);
				Debug.Assert(MinSampleCount <= this.sampleValues.Count);

				// get the sample index and update the next index
				int index = this.nextSampleIndex++;
				if (this.sampleValues.Count <= this.nextSampleIndex) {
					this.nextSampleIndex = 0;
				}

				return this.sampleValues[index];
			}

		#endregion


		#region overrides

			protected virtual bool AreEqualValues(T value1, T value2) {
				return object.Equals(value1, value2);
			}

			protected abstract T GetValueAdapter(IObjectData data, string name, T defaultValue);

			protected abstract void SetValueAdapter(IObjectData data, string name, T value, bool omitDefault = false, bool isDefault = false);

		#endregion
		}

		public abstract class ValueTestBase<T> {
		#region data

			public const int MinSampleCount = 2;


			protected readonly Func<IObjectData> createEmptyObjectData;

			protected readonly IReadOnlyList<T> sampleValues;

			private int nextSampleIndex = 0;

		#endregion


		#region creation and disposal

			protected ValueTestBase(Func<IObjectData> createEmptyObjectData, T[] sampleValues) {
				// argument checks
				if (createEmptyObjectData == null) {
					throw new ArgumentNullException(nameof(createEmptyObjectData));
				}
				if (sampleValues == null || sampleValues.Length < MinSampleCount) {
					throw new ArgumentException($"It must contain more than {MinSampleCount} items.", nameof(sampleValues));
				}
				if (HasEqualValues(sampleValues)) {
					throw new ArgumentException("There are more than two equal items.", nameof(sampleValues));
				}

				// initialize members
				this.createEmptyObjectData = createEmptyObjectData;
				this.sampleValues = sampleValues;
				Debug.Assert(this.nextSampleIndex == 0);
			}

		#endregion


		#region tests

			[Fact(DisplayName = "Defaultable, Basic")]
			public void Defaultable_Basic() {
				// ARRANGE
				string name = "Value";
				T defaultValue = GetSampleValue();
				T value = GetSampleValue();
				Debug.Assert(AreEqualValues(value, defaultValue) == false);

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, value);
				T actual = GetValueAdapter(data, name, defaultValue);

				// ASSERT
				// Note that AreEqualValues() should be used to compare values
				Assert.Equal(true, AreEqualValues(value, actual));
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Defaultable, Overriding")]
			public void Defaultable_Overriding() {
				// ARRANGE
				string name = "Value";
				T originalValue = GetSampleValue();
				T defaultValue = originalValue;
				T value = GetSampleValue();
				Debug.Assert(AreEqualValues(value, defaultValue) == false);
				Debug.Assert(AreEqualValues(value, originalValue) == false);

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, originalValue);
				SetValueAdapter(data, name, value);
				T actual = GetValueAdapter(data, name, defaultValue);

				// ASSERT
				// Note that AreEqualValues() should be used to compare values
				Assert.Equal(true, AreEqualValues(value, actual));
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Defaultable, Overriding by omission")]
			public void Defaultable_OverridingByOmission() {
				// ARRANGE
				string name = "Value";
				T defaultValue = GetSampleValue();
				T originalValue = GetSampleValue();
				T value = defaultValue;
				Debug.Assert(AreEqualValues(value, defaultValue));
				Debug.Assert(AreEqualValues(value, originalValue) == false);

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, originalValue);
				SetValueAdapter(data, name, value, omitDefault: true, isDefault: true);	// omission
				T actual = GetValueAdapter(data, name, defaultValue);

				// ASSERT
				// Note that AreEqualValues() should be used to compare values
				Assert.Equal(true, AreEqualValues(value, actual));
				Assert.Equal(0, data.Names.Count());
				Assert.Equal(null, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Defaultable, omitDefault: false, isDefault: true")]
			public void Defaultable_Args_omitDefault_false_isDefault_true() {
				// ARRANGE
				string name = "Value";
				T defaultValue = GetSampleValue();
				T value = GetSampleValue();
				Debug.Assert(AreEqualValues(value, defaultValue) == false);

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, value, omitDefault: false, isDefault: true);
				T actual = GetValueAdapter(data, name, defaultValue);

				// ASSERT
				// Note that AreEqualValues() should be used to compare values
				Assert.Equal(true, AreEqualValues(value, actual));
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Defaultable, omitDefault: true, isDefault: false")]
			public void Defaultable_Args_omitDefault_true_isDefault_false() {
				// ARRANGE
				string name = "Value";
				T defaultValue = GetSampleValue();
				T value = GetSampleValue();
				Debug.Assert(AreEqualValues(value, defaultValue) == false);

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, value, omitDefault: true, isDefault: false);
				T actual = GetValueAdapter(data, name, defaultValue);

				// ASSERT
				// Note that AreEqualValues() should be used to compare values
				Assert.Equal(true, AreEqualValues(value, actual));
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Defaultable, omitDefault: true, isDefault: true")]
			public void Defaultable_Args_omitDefault_true_isDefault_true() {
				// ARRANGE
				string name = "Value";
				T defaultValue = GetSampleValue();
				T value = defaultValue;
				Debug.Assert(AreEqualValues(value, defaultValue));

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, value, omitDefault: true, isDefault: true);
				T actual = GetValueAdapter(data, name, defaultValue);

				// ASSERT
				// Note that AreEqualValues() should be used to compare values
				Assert.Equal(true, AreEqualValues(value, actual));
				Assert.Equal(0, data.Names.Count());
				Assert.Equal(null, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Defaultable, name: null")]
			public void Defaultable_Args_name_null() {
				IObjectData data = CreateEmptyObjectData();
				T defaultValue = GetSampleValue();
				T value = GetSampleValue();

				Assert.Throws<ArgumentNullException>(
					() => {
						GetValueAdapter(data, null, defaultValue);
					}
				);
				Assert.Throws<ArgumentNullException>(
					() => {
						SetValueAdapter(data, null, value);
					}
				);
			}

		#endregion


		#region methods

			protected IObjectData CreateEmptyObjectData() {
				// state checks
				Debug.Assert(this.createEmptyObjectData != null);

				return this.createEmptyObjectData();
			}

			protected bool HasEqualValues(T[] sampleValues) {
				// argument checks
				Debug.Assert(sampleValues != null);

				// check whether there are items which have the equal value.
				for (int i = 0; i < sampleValues.Length; ++i) {
					T t = sampleValues[i];
					for (int j = i + 1; j < sampleValues.Length; ++j) {
						if (AreEqualValues(t, sampleValues[j])) {
							return true;
						}
					}
				}

				return false;
			}

			protected T GetSampleValue() {
				// state checks
				Debug.Assert(this.sampleValues != null);
				Debug.Assert(MinSampleCount <= this.sampleValues.Count);

				// get the sample index and update the next index
				int index = this.nextSampleIndex++;
				if (this.sampleValues.Count <= this.nextSampleIndex) {
					this.nextSampleIndex = 0;
				}

				return this.sampleValues[index];
			}

		#endregion


		#region overrides

			protected virtual bool AreEqualValues(T value1, T value2) {
				return object.Equals(value1, value2);
			}

			protected abstract T GetValueAdapter(IObjectData data, string name, T defaultValue);

			protected abstract void SetValueAdapter(IObjectData data, string name, T value, bool omitDefault = false, bool isDefault = false);

		#endregion
		}

		public abstract class ValueTestForValueType<T>: ValueTestBase<T> where T : struct {
		#region creation and disposal

			protected ValueTestForValueType(Func<IObjectData> createEmptyObjectData, T[] sampleValues): base(createEmptyObjectData, sampleValues) {
			}

		#endregion


		#region tests

			[Fact(DisplayName = "Getting null value")]
			public void NullValue() {
				// ARRANGE
				string name = "Value";
				T defaultValue = GetSampleValue();

				// ACT
				IObjectData data = CreateEmptyObjectData();
				data.SetObjectValue(name, null, omitDefault: false, isDefault: false);

				// ASSERT
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
				Assert.Throws<FormatException>(
					() => {
						GetValueAdapter(data, name);
					}
				);
				Assert.Throws<FormatException>(
					() => {
						GetValueAdapter(data, name, defaultValue);
					}
				);
			}

			[Fact(DisplayName = "Explicit, Basic")]
			public void Explicit_Basic() {
				// ARRANGE
				string name = "Value";
				T? value = GetSampleValue();

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, value);
				T? actual = GetValueAdapter(data, name);

				// ASSERT
				// Note that AreEqualNullables() should be used to compare values
				Assert.Equal(true, AreEqualNullables(value, actual));
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Explicit, Overriding")]
			public void Explicit_Overriding() {
				// ARRANGE
				string name = "Value";
				T? originalValue = GetSampleValue();
				T? value = GetSampleValue();
				Debug.Assert(AreEqualValues(value.Value, originalValue.Value) == false);

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, originalValue);
				SetValueAdapter(data, name, value);
				T? actual = GetValueAdapter(data, name);

				// ASSERT
				// Note that AreEqualNullables() should be used to compare values
				Assert.Equal(true, AreEqualNullables(value, actual));
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Explicit, Overriding by omission")]
			public void Explicit_OverridingByOmission() {
				// ARRANGE
				string name = "Value";
				T? originalValue = GetSampleValue();
				T? value = null;

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, originalValue);
				SetValueAdapter(data, name, value);
				T? actual = GetValueAdapter(data, name);

				// ASSERT
				// Note that AreEqualNullables() should be used to compare values
				Assert.Equal(true, AreEqualNullables(value, actual));
				Assert.Equal(0, data.Names.Count());
				Assert.Equal(null, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Explicit, value: null")]
			public void Explicit_Args_value_null() {
				// ARRANGE
				string name = "Value";
				T? value = null;

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, value);
				T? actual = GetValueAdapter(data, name);

				// ASSERT
				// Note that AreEqualNullables() should be used to compare values
				Assert.Equal(true, AreEqualNullables(value, actual));
				Assert.Equal(0, data.Names.Count());
				Assert.Equal(null, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Explicit, name: null")]
			public void Explicit_Args_name_null() {
				IObjectData data = CreateEmptyObjectData();
				T? value = GetSampleValue();

				Assert.Throws<ArgumentNullException>(
					() => {
						GetValueAdapter(data, null);
					}
				);
				Assert.Throws<ArgumentNullException>(
					() => {
						SetValueAdapter(data, null, value);
					}
				);
			}

		#endregion


		#region methods

			protected bool AreEqualNullables(T? value1, T? value2) {
				return (value1 == null) ? (value2 == null) : (value2 != null && AreEqualValues(value1.Value, value2.Value));
			}

		#endregion


		#region overrides

			protected abstract T? GetValueAdapter(IObjectData data, string name);

			protected abstract void SetValueAdapter(IObjectData data, string name, T? value);

		#endregion
		}

		public abstract class ValueTestForReferenceType<T>: ValueTestBase<T> where T : class {
		#region creation and disposal

			protected ValueTestForReferenceType(Func<IObjectData> createEmptyObjectData, T[] sampleValues) : base(createEmptyObjectData, sampleValues) {
			}

		#endregion


		#region tests

			[Fact(DisplayName = "Defaultable, Getting null value")]
			public void Defaultable_NullValue() {
				// ARRANGE
				string name = "Value";
				T defaultValue = GetSampleValue().Item1;

				// ACT
				IObjectData data = CreateEmptyObjectData();
				data.SetObjectValue(name, null, omitDefault: false, isDefault: false);
				T actual = GetValueAdapter(data, name, defaultValue);

				// ASSERT
				Assert.Equal(null, actual);
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Explicit, Getting null value")]
			public void Explicit_NullValue() {
				// ARRANGE
				string name = "Value";

				// ACT
				IObjectData data = CreateEmptyObjectData();
				data.SetObjectValue(name, null, omitDefault: false, isDefault: false);
				Tuple<T> actual = GetValueAdapter(data, name);

				// ASSERT
				Assert.Equal(new Tuple<T>(null), actual);
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Explicit, Basic")]
			public void Explicit_Basic() {
				// ARRANGE
				string name = "Value";
				Tuple<T> value = GetSampleValue();

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, value);
				Tuple<T> actual = GetValueAdapter(data, name);

				// ASSERT
				// Note that AreEqualTuples() should be used to compare values
				Assert.Equal(true, AreEqualTuples(value, actual));
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Explicit, Overriding")]
			public void Explicit_Overriding() {
				// ARRANGE
				string name = "Value";
				Tuple<T> originalValue = GetSampleValue();
				Tuple<T> value = GetSampleValue();
				Debug.Assert(AreEqualValues(value.Item1, originalValue.Item1) == false);

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, originalValue);
				SetValueAdapter(data, name, value);
				Tuple<T> actual = GetValueAdapter(data, name);

				// ASSERT
				// Note that AreEqualTuples() should be used to compare values
				Assert.Equal(true, AreEqualTuples(value, actual));
				Assert.Equal(1, data.Names.Count());
				Assert.Equal(name, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Explicit, Overriding by omission")]
			public void Explicit_OverridingByOmission() {
				// ARRANGE
				string name = "Value";
				Tuple<T> originalValue = GetSampleValue();
				Tuple<T> value = null;

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, originalValue);
				SetValueAdapter(data, name, value);
				Tuple<T> actual = GetValueAdapter(data, name);

				// ASSERT
				// Note that AreEqualTuples() should be used to compare values
				Assert.Equal(true, AreEqualTuples(value, actual));
				Assert.Equal(0, data.Names.Count());
				Assert.Equal(null, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Explicit, value: null")]
			public void Explicit_Args_value_null() {
				// ARRANGE
				string name = "Value";
				Tuple<T> value = null;

				// ACT
				IObjectData data = CreateEmptyObjectData();
				SetValueAdapter(data, name, value);
				Tuple<T> actual = GetValueAdapter(data, name);

				// ASSERT
				// Note that AreEqualTuples() should be used to compare values
				Assert.Equal(true, AreEqualTuples(value, actual));
				Assert.Equal(0, data.Names.Count());
				Assert.Equal(null, data.Names.FirstOrDefault());
			}

			[Fact(DisplayName = "Explicit, name: null")]
			public void Explicit_Args_name_null() {
				IObjectData data = CreateEmptyObjectData();
				Tuple<T> value = GetSampleValue();

				Assert.Throws<ArgumentNullException>(
					() => {
						GetValueAdapter(data, null);
					}
				);
				Assert.Throws<ArgumentNullException>(
					() => {
						SetValueAdapter(data, null, value);
					}
				);
			}

		#endregion


		#region methods

			protected bool AreEqualTuples(Tuple<T> value1, Tuple<T> value2) {
				return (value1 == null) ? (value2 == null) : (value2 != null && AreEqualValues(value1.Item1, value2.Item1));
			}

			protected new Tuple<T> GetSampleValue() {
				return new Tuple<T>(base.GetSampleValue());
			}

		#endregion


		#region overrides

			protected abstract Tuple<T> GetValueAdapter(IObjectData data, string name);

			protected abstract void SetValueAdapter(IObjectData data, string name, Tuple<T> value);

		#endregion
		}


		public abstract class Int32ValueTestBase: ValueTestForValueType<int> {
		#region data

			protected static readonly int[] DefaultSampleValues = new int[] { 1, -4, 0, 19, 33, -130, -1, 592, -81 };

		#endregion


		#region creation and disposal

			protected Int32ValueTestBase(Func<IObjectData> createEmptyObjectData, int[] sampleValues): base(createEmptyObjectData, sampleValues) {
			}

			protected Int32ValueTestBase(Func<IObjectData> createEmptyObjectData): this(createEmptyObjectData, DefaultSampleValues) {
			}

		#endregion


		#region overrides

			protected override bool AreEqualValues(int value1, int value2) {
				return value1 == value2;
			}

			protected override int GetValueAdapter(IObjectData data, string name, int defaultValue) {
				return data.GetInt32Value(name, defaultValue);
			}

			protected override void SetValueAdapter(IObjectData data, string name, int value, bool omitDefault, bool isDefault) {
				data.SetInt32Value(name, value, omitDefault, isDefault);				
			}

			protected override int? GetValueAdapter(IObjectData data, string name) {
				return data.GetInt32Value(name);
			}

			protected override void SetValueAdapter(IObjectData data, string name, int? value) {
				data.SetInt32Value(name, value);
			}

		#endregion
		}

		public abstract class BooleanValueTestBase: ValueTestForValueType<bool> {
		#region data

			protected static readonly bool[] DefaultSampleValues = new bool[] { true, false };

		#endregion


		#region creation and disposal

			protected BooleanValueTestBase(Func<IObjectData> createEmptyObjectData, bool[] sampleValues) : base(createEmptyObjectData, sampleValues) {
			}

			protected BooleanValueTestBase(Func<IObjectData> createEmptyObjectData) : this(createEmptyObjectData, DefaultSampleValues) {
			}

		#endregion


		#region overrides

			protected override bool AreEqualValues(bool value1, bool value2) {
				return value1 == value2;
			}

			protected override bool GetValueAdapter(IObjectData data, string name, bool defaultValue) {
				return data.GetBooleanValue(name, defaultValue);
			}

			protected override void SetValueAdapter(IObjectData data, string name, bool value, bool omitDefault, bool isDefault) {
				data.SetBooleanValue(name, value, omitDefault, isDefault);
			}

			protected override bool? GetValueAdapter(IObjectData data, string name) {
				return data.GetBooleanValue(name);
			}

			protected override void SetValueAdapter(IObjectData data, string name, bool? value) {
				data.SetBooleanValue(name, value);
			}

		#endregion
		}

		public abstract class StringValueTestBase: ValueTestForReferenceType<string> {
		#region data

			protected static readonly string[] DefaultSampleValues = new string[] { "A", "", "XYZ", "あいう", "-+" };

		#endregion


		#region creation and disposal

			protected StringValueTestBase(Func<IObjectData> createEmptyObjectData, string[] sampleValues) : base(createEmptyObjectData, sampleValues) {
			}

			protected StringValueTestBase(Func<IObjectData> createEmptyObjectData) : this(createEmptyObjectData, DefaultSampleValues) {
			}

		#endregion


		#region overrides

			protected override bool AreEqualValues(string value1, string value2) {
				return string.CompareOrdinal(value1, value2) == 0;
			}

			protected override string GetValueAdapter(IObjectData data, string name, string defaultValue) {
				return data.GetStringValue(name, defaultValue);
			}

			protected override void SetValueAdapter(IObjectData data, string name, string value, bool omitDefault, bool isDefault) {
				data.SetStringValue(name, value, omitDefault, isDefault);
			}

			protected override Tuple<string> GetValueAdapter(IObjectData data, string name) {
				return data.GetStringValue(name);
			}

			protected override void SetValueAdapter(IObjectData data, string name, Tuple<string> value) {
				data.SetStringValue(name, value);
			}

		#endregion
		}

		public abstract class ObjectValueTestBase: ValueTestForReferenceType<IObjectData> {
		#region creation and disposal

			protected ObjectValueTestBase(Func<IObjectData> createEmptyObjectData, IObjectData[] sampleValues) : base(createEmptyObjectData, GetSampleValues(createEmptyObjectData, sampleValues)) {
			}

			protected ObjectValueTestBase(Func<IObjectData> createEmptyObjectData) : this(createEmptyObjectData, null) {
			}

		#endregion


		#region methods

			protected static IObjectData[] GetDefaultSampleValues(Func<IObjectData> createEmptyObjectData) {
				// argument checks
				if (createEmptyObjectData == null) {
					throw new ArgumentNullException(nameof(createEmptyObjectData));
				}

				List<IObjectData> buf = new List<IObjectData>();
				IObjectData data, subData;

				data = createEmptyObjectData();
				data.SetInt32Value("Value1", 30);
				buf.Add(data);

				data = createEmptyObjectData();
				data.SetStringValue("Value2", "ABC");
				buf.Add(data);

				data = createEmptyObjectData();
				subData = createEmptyObjectData();
				subData.SetDoubleValue("Value3-1", 3.14);
				subData.SetBooleanValue("Value3-2", true);
				data.SetObjectValue("Value3", subData);
				buf.Add(data);

				data = createEmptyObjectData();
				data.SetBooleanValue("Value4", true);
				buf.Add(data);

				return buf.ToArray();
			}

		#endregion


		#region overrides

			protected override bool AreEqualValues(IObjectData value1, IObjectData value2) {
				return (value1 == null) ? (value2 == null) : value1.Equals(value2);
			}

			protected override IObjectData GetValueAdapter(IObjectData data, string name, IObjectData defaultValue) {
				return data.GetObjectValue(name, defaultValue);
			}

			protected override void SetValueAdapter(IObjectData data, string name, IObjectData value, bool omitDefault, bool isDefault) {
				data.SetObjectValue(name, value, omitDefault, isDefault);
			}

			protected override Tuple<IObjectData> GetValueAdapter(IObjectData data, string name) {
				return data.GetObjectValue(name);
			}

			protected override void SetValueAdapter(IObjectData data, string name, Tuple<IObjectData> value) {
				data.SetObjectValue(name, value);
			}

		#endregion


		#region privates

			private static IObjectData[] GetSampleValues(Func<IObjectData> createEmptyObjectData, IObjectData[] sampleValues) {
				return sampleValues ?? GetDefaultSampleValues(createEmptyObjectData);
			}

		#endregion
		}

		public abstract class EnumValueTestBase: ValueTestForValueType<DayOfWeek> {
		#region data

			protected static readonly DayOfWeek[] DefaultSampleValues = new DayOfWeek[] {
				DayOfWeek.Sunday,
				DayOfWeek.Monday,
				DayOfWeek.Tuesday,
				DayOfWeek.Wednesday,
				DayOfWeek.Thursday,
				DayOfWeek.Friday,
				DayOfWeek.Saturday
			};

		#endregion


		#region creation and disposal

			protected EnumValueTestBase(Func<IObjectData> createEmptyObjectData, DayOfWeek[] sampleValues) : base(createEmptyObjectData, sampleValues) {
			}

			protected EnumValueTestBase(Func<IObjectData> createEmptyObjectData) : this(createEmptyObjectData, DefaultSampleValues) {
			}

		#endregion


		#region overrides

			protected override bool AreEqualValues(DayOfWeek value1, DayOfWeek value2) {
				return value1 == value2;
			}

			protected override DayOfWeek GetValueAdapter(IObjectData data, string name, DayOfWeek defaultValue) {
				return (DayOfWeek)data.GetEnumValue(name, typeof(DayOfWeek), defaultValue);
			}

			protected override void SetValueAdapter(IObjectData data, string name, DayOfWeek value, bool omitDefault, bool isDefault) {
				data.SetEnumValue(name, value, omitDefault, isDefault);
			}

			protected override DayOfWeek? GetValueAdapter(IObjectData data, string name) {
				object value = data.GetEnumValue(name, typeof(DayOfWeek)) ;
				return (value == null) ? null : new DayOfWeek?((DayOfWeek)value);
			}

			protected override void SetValueAdapter(IObjectData data, string name, DayOfWeek? value) {
				object actualValue = (value == null) ? null : (object)value.Value;
				data.SetEnumValue(name, actualValue);
			}

		#endregion
		}


		public abstract class Int32ArrayValueTestBase: ValueTestForReferenceType<int[]> {
		#region data

			protected static readonly int[][] DefaultSampleValues = new int[][] {
				new int[] { 1, 2, 3 },
				new int[] { },
				new int[] { -5, 3, 9, 128, -999},
				new int[] {-1 }
			};

		#endregion


		#region creation and disposal

			protected Int32ArrayValueTestBase(Func<IObjectData> createEmptyObjectData, int[][] sampleValues) : base(createEmptyObjectData, sampleValues) {
			}

			protected Int32ArrayValueTestBase(Func<IObjectData> createEmptyObjectData) : this(createEmptyObjectData, DefaultSampleValues) {
			}

		#endregion


		#region overrides

			protected override bool AreEqualValues(int[] value1, int[] value2) {
				return AreEqualArrays(value1, value2, (item1, item2) => item1 == item2);
			}

			protected override int[] GetValueAdapter(IObjectData data, string name, int[] defaultValue) {
				return data.GetInt32ArrayValue(name, defaultValue);
			}

			protected override void SetValueAdapter(IObjectData data, string name, int[] value, bool omitDefault, bool isDefault) {
				data.SetInt32ArrayValue(name, value, omitDefault, isDefault);
			}

			protected override Tuple<int[]> GetValueAdapter(IObjectData data, string name) {
				return data.GetInt32ArrayValue(name);
			}

			protected override void SetValueAdapter(IObjectData data, string name, Tuple<int[]> value) {
				data.SetInt32ArrayValue(name, value);
			}

		#endregion
		}

		public abstract class BooleanArrayValueTestBase: ValueTestForReferenceType<bool[]> {
		#region data

			protected static readonly bool[][] DefaultSampleValues = new bool[][] {
				new bool[] { true, false },
				new bool[] { false },
				new bool[] { },
				new bool[] { false, false },
				new bool[] { false, true, true, false }
			};

		#endregion


		#region creation and disposal

			protected BooleanArrayValueTestBase(Func<IObjectData> createEmptyObjectData, bool[][] sampleValues) : base(createEmptyObjectData, sampleValues) {
			}

			protected BooleanArrayValueTestBase(Func<IObjectData> createEmptyObjectData) : this(createEmptyObjectData, DefaultSampleValues) {
			}

		#endregion


		#region overrides

			protected override bool AreEqualValues(bool[] value1, bool[] value2) {
				return AreEqualArrays(value1, value2, (item1, item2) => item1 == item2);
			}

			protected override bool[] GetValueAdapter(IObjectData data, string name, bool[] defaultValue) {
				return data.GetBooleanArrayValue(name, defaultValue);
			}

			protected override void SetValueAdapter(IObjectData data, string name, bool[] value, bool omitDefault, bool isDefault) {
				data.SetBooleanArrayValue(name, value, omitDefault, isDefault);
			}

			protected override Tuple<bool[]> GetValueAdapter(IObjectData data, string name) {
				return data.GetBooleanArrayValue(name);
			}

			protected override void SetValueAdapter(IObjectData data, string name, Tuple<bool[]> value) {
				data.SetBooleanArrayValue(name, value);
			}

		#endregion
		}

		public abstract class StringArrayValueTestBase: ValueTestForReferenceType<string[]> {
		#region data

			protected static readonly string[][] DefaultSampleValues = new string[][] {
				new string[] { "ABC", "DEF", "GHI"},
				new string[] { },
				new string[] { "12", "Z", "あいうえお"},
				new string[] { "", "", "" },
			};

		#endregion


		#region creation and disposal

			protected StringArrayValueTestBase(Func<IObjectData> createEmptyObjectData, string[][] sampleValues) : base(createEmptyObjectData, sampleValues) {
			}

			protected StringArrayValueTestBase(Func<IObjectData> createEmptyObjectData) : this(createEmptyObjectData, DefaultSampleValues) {
			}

		#endregion


		#region overrides

			protected override bool AreEqualValues(string[] value1, string[] value2) {
				return AreEqualArrays(value1, value2, (item1, item2) => string.CompareOrdinal(item1, item2) == 0);
			}

			protected override string[] GetValueAdapter(IObjectData data, string name, string[] defaultValue) {
				return data.GetStringArrayValue(name, defaultValue);
			}

			protected override void SetValueAdapter(IObjectData data, string name, string[] value, bool omitDefault, bool isDefault) {
				data.SetStringArrayValue(name, value, omitDefault, isDefault);
			}

			protected override Tuple<string[]> GetValueAdapter(IObjectData data, string name) {
				return data.GetStringArrayValue(name);
			}

			protected override void SetValueAdapter(IObjectData data, string name, Tuple<string[]> value) {
				data.SetStringArrayValue(name, value);
			}

		#endregion
		}

		// ToDo: other types, invalid format cases

#endif
		#endregion
	}
}
