using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace MAPE.Utils {
	public static class ObjectDataExtension {
		#region accessors

		public static int ExtractInt32Value(IObjectDataValue value) {
			// argument checks
			Debug.Assert(value != null);

			return value.ExtractInt32Value();
		}

		public static IObjectDataValue CreateInt32Value(IObjectData objectData, int value) {
			// argument checks
			Debug.Assert(objectData != null);

			return objectData.CreateValue(value);
		}

		public static long ExtractInt64Value(IObjectDataValue value) {
			// argument checks
			Debug.Assert(value != null);

			return value.ExtractInt64Value();
		}

		public static IObjectDataValue CreateInt64Value(IObjectData objectData, long value) {
			// argument checks
			Debug.Assert(objectData != null);

			return objectData.CreateValue(value);
		}

		public static double ExtractDoubleValue(IObjectDataValue value) {
			// argument checks
			Debug.Assert(value != null);

			return value.ExtractDoubleValue();
		}

		public static IObjectDataValue CreateDoubleValue(IObjectData objectData, double value) {
			// argument checks
			Debug.Assert(objectData != null);

			return objectData.CreateValue(value);
		}

		public static bool ExtractBooleanValue(IObjectDataValue value) {
			// argument checks
			Debug.Assert(value != null);

			return value.ExtractBooleanValue();
		}

		public static IObjectDataValue CreateBooleanValue(IObjectData objectData, bool value) {
			// argument checks
			Debug.Assert(objectData != null);

			return objectData.CreateValue(value);
		}

		public static string ExtractStringValue(IObjectDataValue value) {
			// argument checks
			Debug.Assert(value != null);

			return value.ExtractStringValue();
		}

		public static IObjectDataValue CreateStringValue(IObjectData objectData, string value) {
			// argument checks
			Debug.Assert(objectData != null);

			return objectData.CreateValue(value);
		}

		public static IObjectData ExtractObjectValue(IObjectDataValue value) {
			// argument checks
			Debug.Assert(value != null);

			return value.ExtractObjectValue();
		}

		public static IObjectDataValue CreateObjectValue(IObjectData objectData, IObjectData value) {
			// argument checks
			Debug.Assert(objectData != null);

			return objectData.CreateValue(value);
		}

		public static IEnumerable<IObjectDataValue> ExtractArrayValue(IObjectDataValue value) {
			// argument checks
			Debug.Assert(value != null);

			return value.ExtractArrayValue();
		}

		public static IObjectDataValue CreateArrayValue(IObjectData objectData, IEnumerable<IObjectDataValue> value) {
			// argument checks
			Debug.Assert(objectData != null);

			return objectData.CreateValue(value);
		}

		#endregion


		#region extensions - base

		public static Out GetValue<In, Out>(this IObjectData data, string name, In defaultValue, Func<IObjectDataValue, Out> extractValue) where Out: In {
			// argument checks
			if (data == null) {
				throw new ArgumentNullException(nameof(data));
			}
			if (extractValue == null) {
				throw new ArgumentNullException(nameof(extractValue));
			}

			// get the value
			IObjectDataValue value = data.GetValue(name);
			return (value == null) ? (Out)defaultValue : extractValue(value);
		}

		public static T? GetValue<T>(this IObjectData data, string name, Func<IObjectDataValue, T> extractValue) where T : struct {
			// argument checks
			if (data == null) {
				throw new ArgumentNullException(nameof(data));
			}
			if (extractValue == null) {
				throw new ArgumentNullException(nameof(extractValue));
			}

			// get the value
			IObjectDataValue value = data.GetValue(name);
			return (value == null) ? null : new Nullable<T>(extractValue(value));
		}

		public static IObjectDataValue CreateValue<T>(this IObjectData data, T value, Func<IObjectData, T, IObjectDataValue> createValue, bool omitDefault = false, bool isDefault = false) {
			// argument checks
			if (data == null) {
				throw new ArgumentNullException(nameof(data));
			}
			if (createValue == null) {
				throw new ArgumentNullException(nameof(createValue));
			}

			// create the value
			return (omitDefault && isDefault) ? null : createValue(data, value);
		}

		public static void SetValue<T>(this IObjectData data, string name, T value, Func<IObjectData, T, IObjectDataValue> createValue, bool omitDefault = false, bool isDefault = false) {
			// set the value
			// Note that null value means "remove the value".
			// The value is given as not IObjectDataValue but T and convert on demand,
			// not to create instance in omission case.
			data.SetValue(name, CreateValue(data, value, createValue, omitDefault, isDefault));
		}

		public static void SetValue<T>(this IObjectData data, string name, T? value, Func<IObjectData, T, IObjectDataValue> createValue) where T : struct{
			// set the value
			data.SetValue(name, (value == null)? null: CreateValue(data, value.Value, createValue, false, false));
		}

		#endregion


		#region extensions - basic types

		public static int GetInt32Value(this IObjectData data, string name, int defaultValue) {
			return GetValue(data, name, defaultValue, ExtractInt32Value);
		}

		public static void SetInt32Value(this IObjectData data, string name, int value, bool omitDefault = false, bool isDefault = false) {
			SetValue(data, name, value, CreateInt32Value, omitDefault, isDefault);
		}

		public static long GetInt64Value(this IObjectData data, string name, long defaultValue) {
			return GetValue(data, name, defaultValue, ExtractInt64Value);
		}

		public static void SetInt64Value(this IObjectData data, string name, long value, bool omitDefault = false, bool isDefault = false) {
			SetValue(data, name, value, CreateInt64Value, omitDefault, isDefault);
		}

		public static double GetDoubleValue(this IObjectData data, string name, double defaultValue) {
			return GetValue(data, name, defaultValue, ExtractDoubleValue);
		}

		public static void SetDoubleValue(this IObjectData data, string name, double value, bool omitDefault = false, bool isDefault = false) {
			SetValue(data, name, value, CreateDoubleValue, omitDefault, isDefault);
		}

		public static bool GetBooleanValue(this IObjectData data, string name, bool defaultValue) {
			return GetValue(data, name, defaultValue, ExtractBooleanValue);
		}

		public static void SetBooleanValue(this IObjectData data, string name, bool value, bool omitDefault = false, bool isDefault = false) {
			SetValue(data, name, value, CreateBooleanValue, omitDefault, isDefault);
		}

		public static string GetStringValue(this IObjectData data, string name, string defaultValue) {
			return GetValue(data, name, defaultValue, ExtractStringValue);
		}

		public static void SetStringValue(this IObjectData data, string name, string value, bool omitDefault = false, bool isDefault = false) {
			SetValue(data, name, value, CreateStringValue, omitDefault, isDefault);
		}

		public static IObjectData GetObjectValue(this IObjectData data, string name, IObjectData defaultValue) {
			return GetValue(data, name, defaultValue, ExtractObjectValue);
		}

		public static void SetObjectValue(this IObjectData data, string name, IObjectData value, bool omitDefault = false, bool isDefault = false) {
			SetValue(data, name, value, CreateObjectValue, omitDefault, isDefault);
		}

		public static IEnumerable<IObjectDataValue> GetArrayValue(this IObjectData data, string name, IEnumerable<IObjectDataValue> defaultValue) {
			return GetValue(data, name, defaultValue, ExtractArrayValue);
		}

		public static void SetArrayValue(this IObjectData data, string name, IEnumerable<IObjectDataValue> value, bool omitDefault = false, bool isDefault = false) {
			SetValue(data, name, value, CreateArrayValue, omitDefault, isDefault);
		}

		#endregion


		#region extensions - enum

		public static Enum ExtractEnumValue(this IObjectDataValue value, Type enumType) {
			// argument checks
			Debug.Assert(value != null);
			Debug.Assert(enumType != null);

			try {
				return (Enum)Enum.Parse(enumType, value.ExtractStringValue());
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}
		}

		public static Enum GetEnumValue(this IObjectData data, string name, Enum defaultValue, Type enumType) {
			// argument checks
			if (enumType == null) {
				throw new ArgumentNullException(nameof(enumType));
			}

			return GetValue(data, name, defaultValue, value => ExtractEnumValue(value, enumType));
		}

		public static IObjectDataValue CreateEnumValue(this IObjectData data, Enum value) {
			// argument checks
			Debug.Assert(data != null);
			Debug.Assert(value != null);

			return data.CreateValue(value.ToString());
		}

		public static void SetEnumValue(this IObjectData data, string name, Enum value, bool omitDefault = false, bool isDefault = false) {
			SetValue(data, name, value, CreateEnumValue, omitDefault, isDefault);
		}

		#endregion


		#region extensions - general object

		public static T GetObjectValue<T>(this IObjectData data, string name, T defaultValue, Func<IObjectData, T> createObject) {
			// argument checks
			if (createObject == null) {
				throw new ArgumentNullException(nameof(createObject));
			}

			return GetValue(data, name, defaultValue, v => createObject(v.ExtractObjectValue()));
		}

		public static T? GetNullableValue<T>(this IObjectData data, string name, Func<IObjectData, T> createObject) where T : struct {
			// argument checks
			if (createObject == null) {
				throw new ArgumentNullException(nameof(createObject));
			}

			return GetValue(data, name, v => createObject(v.ExtractObjectValue()));
		}


		public static IObjectDataValue CreateObjectValue<T>(this IObjectData data, T value, Action<T, IObjectData, bool> saveObject, string name, bool overwrite, bool omitDefault) {
			// argument checks
			Debug.Assert(data != null);
			Debug.Assert(saveObject != null);
			Debug.Assert(name != null || overwrite == false);

			IObjectData objectData = null;
			if (value != null) {
				// get IObjectData into which the value saves its contents
				if (overwrite) {
					// overwirte mode
					objectData = data.GetObjectValue(name, defaultValue: null);
				}
				if (objectData == null) {
					// create a empty IObjectData
					objectData = data.CreateObject();
				}

				// save the value's contents
				saveObject(value, objectData, omitDefault);
			}

			return data.CreateValue(objectData);
		}

		public static void SetObjectValue<T>(this IObjectData data, string name, T value, Action<T, IObjectData, bool> saveObject, bool overwrite = false, bool omitDefault = false, bool isDefault = false) {
			// argument checks
			if (saveObject == null) {
				throw new ArgumentNullException(nameof(saveObject));
			}

			SetValue(data, name, value, (d, v) => CreateObjectValue(d, v, saveObject, name, overwrite, omitDefault), omitDefault, isDefault);
		}

		public static void SetNullableValue<T>(this IObjectData data, string name, T? value, Action<T, IObjectData, bool> saveObject, bool overwrite = false) where T : struct {
			// argument checks
			if (saveObject == null) {
				throw new ArgumentNullException(nameof(saveObject));
			}

			SetValue<T>(data, name, value, (d, v) => CreateObjectValue(d, v, saveObject, name, overwrite, false));
		}


		public static void SaveObject(ISavableToObjectData value, IObjectData data, bool omitDefault = false) {
			// argument chedcks
			Debug.Assert(value != null);
			Debug.Assert(data != null);

			value.SaveToObjectData(data, omitDefault);
		}

		public static void SetObjectValue(this IObjectData data, string name, ISavableToObjectData value, bool overwrite = false, bool omitDefault = false, bool isDefault = false) {
			SetObjectValue(data, name, value, SaveObject, overwrite, omitDefault, isDefault);
		}

		#endregion


		#region extensions - typed array

		public static T[] ExtractArrayValue<T>(this IObjectDataValue value, Func<IObjectDataValue, T> extractItem) {
			// argument checks
			Debug.Assert(value != null);
			Debug.Assert(extractItem != null);

			IEnumerable<IObjectDataValue> itemValues = value.ExtractArrayValue();
			return (itemValues == null) ? null : itemValues.Select(extractItem).ToArray();
		}

		public static T[] GetArrayValue<T>(this IObjectData data, string name, IEnumerable<T> defaultValue, Func<IObjectDataValue, T> extractItem) {
			// argument checks
			if (extractItem == null) {
				throw new ArgumentNullException(nameof(extractItem));
			}

			return GetValue(data, name, defaultValue, v => ExtractArrayValue(v, extractItem));
		}

		public static IObjectDataValue CreateArrayValue<T>(IObjectData data, IEnumerable<T> value, Func<IObjectData, T, IObjectDataValue> createItem) {
			// argument checks
			Debug.Assert(data != null);
			Debug.Assert(createItem != null);

			IEnumerable<IObjectDataValue> actualValue = (value == null) ? null : value.Select(t => createItem(data, t));
			return data.CreateValue(actualValue);
		}

		public static void SetArrayValue<T>(this IObjectData data, string name, IEnumerable<T> value, Func<IObjectData, T, IObjectDataValue> createItem, bool omitDefault = false, bool isDefault = false) {
			// argument checks
			if (createItem == null) {
				throw new ArgumentNullException(nameof(createItem));
			}

			SetValue(data, name, value, (d, v) => CreateArrayValue(d, v, createItem), omitDefault, isDefault);
		}


		public static int[] GetInt32ArrayValue(this IObjectData data, string name, IEnumerable<int> defaultValue) {
			return GetArrayValue(data, name, defaultValue, ExtractInt32Value);
		}

		public static void SetInt32ArrayValue(this IObjectData data, string name, IEnumerable<int> value, bool omitDefault = false, bool isDefault = false) {
			SetArrayValue(data, name, value, CreateInt32Value, omitDefault, isDefault);
		}

		public static long[] GetInt64ArrayValue(this IObjectData data, string name, IEnumerable<long> defaultValue) {
			return GetArrayValue(data, name, defaultValue, ExtractInt64Value);
		}

		public static void SetInt64ArrayValue(this IObjectData data, string name, IEnumerable<long> value, bool omitDefault = false, bool isDefault = false) {
			SetArrayValue(data, name, value, CreateInt64Value, omitDefault, isDefault);
		}

		public static double[] GetDoubleArrayValue(this IObjectData data, string name, IEnumerable<double> defaultValue) {
			return GetArrayValue(data, name, defaultValue, ExtractDoubleValue);
		}

		public static void SetDoubleArrayValue(this IObjectData data, string name, IEnumerable<double> value, bool omitDefault = false, bool isDefault = false) {
			SetArrayValue(data, name, value, CreateDoubleValue, omitDefault, isDefault);
		}

		public static bool[] GetBooleanArrayValue(this IObjectData data, string name, IEnumerable<bool> defaultValue) {
			return GetArrayValue(data, name, defaultValue, ExtractBooleanValue);
		}

		public static void SetBooleanArrayValue(this IObjectData data, string name, IEnumerable<bool> value, bool omitDefault = false, bool isDefault = false) {
			SetArrayValue(data, name, value, CreateBooleanValue, omitDefault, isDefault);
		}

		public static string[] GetBooleanArrayValue(this IObjectData data, string name, IEnumerable<string> defaultValue) {
			return GetArrayValue(data, name, defaultValue, ExtractStringValue);
		}

		public static void SetStringArrayValue(this IObjectData data, string name, IEnumerable<string> value, bool omitDefault = false, bool isDefault = false) {
			SetArrayValue(data, name, value, CreateStringValue, omitDefault, isDefault);
		}

		public static T[] GetObjectArrayValue<T>(this IObjectData data, string name, IEnumerable<T> defaultValue, Func<IObjectData, T> createObject) {
			return GetArrayValue(data, name, defaultValue, v => createObject(v.ExtractObjectValue()));
		}

		public static void SetObjectArrayValue<T>(this IObjectData data, string name, IEnumerable<T> value, Action<T, IObjectData, bool> saveObject, bool omitDefault = false, bool isDefault = false) {
			SetArrayValue(data, name, value, (d, v) => CreateObjectValue(d, v, saveObject, null, false, omitDefault), omitDefault, isDefault);
		}

		public static void SetObjectArrayValue(this IObjectData data, string name, IEnumerable<ISavableToObjectData> value, bool omitDefault = false, bool isDefault = false) {
			SetObjectArrayValue(data, name, value, SaveObject, omitDefault, isDefault);
		}

		#endregion
	}
}
