using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace MAPE.Utils {
	public class JsonObjectData: IObjectData {
		#region types

		private class Value: IObjectDataValue {
			#region data

			public readonly JToken Token;

			#endregion


			#region creation and disposal

			public Value(JToken token) {
				// argument checks
				// token can be null

				// initialize members
				this.Token = token;
			}

			#endregion


			#region IObjectDataValue

			public ObjectDataValueType Type {
				get {
					if (this.Token == null) {
						return ObjectDataValueType.Null;
					} else {
						switch (this.Token.Type) {
							case JTokenType.Object:
								return ObjectDataValueType.Object;
							case JTokenType.Array:
								return ObjectDataValueType.Array;
							case JTokenType.Integer:
								return ObjectDataValueType.Integer;
							case JTokenType.Float:
								return ObjectDataValueType.Float;
							case JTokenType.String:
								return ObjectDataValueType.String;
							case JTokenType.Boolean:
								return ObjectDataValueType.Boolean;
							case JTokenType.Null:
								return ObjectDataValueType.Null;
							default:
								return ObjectDataValueType.Unknown;
						}
					}
				}
			}

			public int ExtractInt32Value() {
				return ExtractValue(token => (int)token);
			}

			public long ExtractInt64Value() {
				return ExtractValue(token => (long)token);
			}

			public double ExtractDoubleValue() {
				return ExtractValue(token => (double)token);
			}

			public bool ExtractBooleanValue() {
				return ExtractValue(token => (bool)token);
			}

			public string ExtractStringValue() {
				return ExtractValue(token => (token.Type == JTokenType.Null) ? null : (string)token);
			}

			public IObjectData ExtractObjectValue() {
				return ExtractValue(token => (token.Type == JTokenType.Null) ? null : new JsonObjectData((JObject)token));
			}

			public IEnumerable<IObjectDataValue> ExtractArrayValue() {
				return ExtractValue(token => (token.Type == JTokenType.Null) ? null : ((JArray)token).Select(t => new Value(t)).ToArray());
			}

			#endregion


			#region privates

			private T ExtractValue<T>(Func<JToken, T> extractor) {
				// argument checks
				Debug.Assert(extractor != null);

				// state checks
				if (this.Token == null) {
					if (typeof(T).IsValueType) {
						throw new FormatException($"The value cannot be converted to the type '{typeof(T).Name}'");
					} else {
						// returns null
						return default(T);
					}
				}

				// Note that GetXValue method should throw a FormatException on error.
				try {
					return extractor(this.Token);
				} catch (Exception exception) {
					throw new FormatException(exception.Message);
				}
			}

			#endregion
		}

		#endregion


		#region data

		private readonly JObject jsonObject = null;

		#endregion


		#region creation and disposal

		private JsonObjectData(JObject jsonObject) {
			// argument checks
			if (jsonObject == null) {
				throw new ArgumentNullException(nameof(jsonObject));
			}

			// initialize members
			this.jsonObject = jsonObject;
		}

		private JsonObjectData(): this(new JObject()) {
		}


		public static JsonObjectData CreateEmpty() {
			return new JsonObjectData();
		}

		#endregion


		#region methods - load & save

		public static JsonObjectData Load(string filePath, bool createIfNotExist) {
			// argument checks
			if (filePath == null) {
				throw new ArgumentNullException(nameof(filePath));
			}

			// load settings from the file
			JObject jsonObject = null;
			try {
				using (TextReader reader = File.OpenText(filePath)) {
					jsonObject = (JObject)JToken.ReadFrom(new JsonTextReader(reader));
				}
			} catch (FileNotFoundException) {
				if (createIfNotExist == false) {
					throw;
				}
				// continue
			} catch (DirectoryNotFoundException) {
				if (createIfNotExist == false) {
					throw;
				}
				Directory.CreateDirectory(Path.GetDirectoryName(filePath));
				// continue
			}

			// create empty object if necessasry
			if (jsonObject == null && createIfNotExist) {
				// create empty one
				jsonObject = new JObject();
				using (TextWriter writer = File.CreateText(filePath)) {
					writer.Write(jsonObject.ToString());
				}
			}

			return new JsonObjectData(jsonObject);
		}

		public void Save(string filePath) {
			// argument checks
			if (filePath == null) {
				throw new ArgumentNullException(nameof(filePath));
			}

			// save settings to the temp file
			using (TextWriter writer = File.CreateText(filePath)) {
				writer.Write(this.jsonObject.ToString());
			}

			return;
		}

		#endregion


		#region IEqualityComparer<IObjectData>

		public bool Equals(IObjectData obj) {
			JsonObjectData that = obj as JsonObjectData;
			return (that == null) ? false : JToken.DeepEquals(this.jsonObject, that.jsonObject);
		}

		#endregion


		#region IObjectDataValueFactory

		public IObjectDataValue CreateValue(long value) {
			return CreateValue(value, v => (JToken)v);
		}

		public IObjectDataValue CreateValue(double value) {
			return CreateValue(value, v => (JToken)v);
		}

		public IObjectDataValue CreateValue(bool value) {
			return CreateValue(value, v => (JToken)v);
		}

		public IObjectDataValue CreateValue(string value) {
			return CreateValue(value, v => (JToken)v);
		}

		public IObjectDataValue CreateValue(IObjectData value) {
			return CreateValue(value, v => ToJObject(value));
		}

		public IObjectDataValue CreateValue(IEnumerable<IObjectDataValue> value) {
			return CreateValue(value, FromArray);
		}

		#endregion


		#region IObjectData

		public IEnumerable<string> GetNames() {
			return this.jsonObject.Properties().Select(prop => prop.Name).ToArray();
		}

		public IObjectDataValue GetValue(string name) {
			// argument checks
			if (name == null) {
				throw new ArgumentNullException(nameof(name));
			}

			// get value
			JToken token = this.jsonObject.GetValue(name);
			return (token == null) ? null : new Value(token);
		}

		public void SetValue(string name, IObjectDataValue value) {
			// argument checks
			if (name == null) {
				throw new ArgumentNullException(nameof(name));
			}
			// value can be null

			// set value
			// if value is null, the name-value pair is removed
			if (value == null) {
				this.jsonObject.Remove(name);
			} else {
				Value actualValue = value as Value;
				if (actualValue == null) {
					throw new ArgumentException("The value is not created by this IObjectData.", nameof(value));
				}

				this.jsonObject[name] = actualValue.Token;
			}
		}

		public bool RemoveValue(string name) {
			// argument checks
			if (name == null) {
				throw new ArgumentNullException(nameof(name));
			}

			// remove the value
			return this.jsonObject.Remove(name);
		}

		public IObjectData CreateObject() {
			return CreateEmpty();
		}

		#endregion


		#region privates

		private IObjectDataValue CreateValue<T>(T value, Func<T, JToken> creator) {
			// argument checks
			Debug.Assert(creator != null);

			// Note that CreateValue method should throw a FormatException on converting error.
			JToken token;
			try {
				token = creator(value);
			} catch (Exception exception) {
				throw new FormatException(exception.Message);
			}

			return new Value(token);
		}

		private static JObject ToJObject(IObjectData value) {
			if (value == null) {
				return null;
			} else {
				JsonObjectData obj = value as JsonObjectData;
				if (obj == null) {
					throw new ArgumentException("The value is not created from this object.");
				}

				return obj.jsonObject;
			}
		}

		private static JToken ToJToken(IObjectDataValue value) {
			if (value == null) {
				return null;
			} else {
				Value actualValue = value as Value;
				if (actualValue == null) {
					throw new ArgumentException("The value is not created from this object.");
				}

				return actualValue.Token;
			}
		}

		private static JToken FromArray(IEnumerable<IObjectDataValue> value) {
			return (value == null)? null: new JArray(value.Select(ToJToken).ToArray());
		}

		#endregion
	}
}
