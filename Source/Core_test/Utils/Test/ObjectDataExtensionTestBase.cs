using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;


namespace MAPE.Utils.Test {
	public class ObjectDataExtensionTestBase {
#if N
		#region utilities
		#endregion


		#region test bases

		public abstract class ValueTypeObjectValueTestBase<T>: ObjectDataTestBase.ValueTestForValueType<T> where T : struct {
			#region data

			protected readonly Func<IObjectData, T> Creator;

			protected readonly Func<T, IObjectDataValueFactory, bool, IObjectData> Formatter;

			#endregion


			#region creation and disposal

			protected ValueTypeObjectValueTestBase(Func<IObjectData> createEmptyObjectData, Func<IObjectData, T> creator, Func<T, IObjectDataValueFactory, bool, IObjectData> formatter, T[] sampleValues) : base(createEmptyObjectData, sampleValues) {
				// argument checks
				if (creator == null) {
					throw new ArgumentNullException(nameof(creator));
				}
				if (formatter == null) {
					throw new ArgumentNullException(nameof(formatter));
				}

				// initialize members
				this.Creator = creator;
				this.Formatter = formatter;
			}

			#endregion


			#region overrides

			protected override T GetValueAdapter(IObjectData data, string name, T defaultValue) {
				return data.GetObjectValue(name, defaultValue, this.Creator);
			}

			protected override void SetValueAdapter(IObjectData data, string name, T value, bool omitDefault, bool isDefault) {
				data.SetObjectValue(name, value, this.Formatter, omitDefault, isDefault);
			}

			protected override T? GetValueAdapter(IObjectData data, string name) {
				return data.GetValueTypeValue(name, this.Creator);
			}

			protected override void SetValueAdapter(IObjectData data, string name, T? value) {
				data.SetValueTypeValue(name, value, this.Formatter);
			}

			#endregion
		}

		public abstract class ReferenceTypeObjectValueTestBase<T>: ObjectDataTestBase.ValueTestForReferenceType<T> where T: class {
			#region data

			protected readonly Func<IObjectData, T> Creator;

			protected readonly Func<T, IObjectDataValueFactory, bool, IObjectData> Formatter;

			#endregion


			#region creation and disposal

			protected ReferenceTypeObjectValueTestBase(Func<IObjectData> createEmptyObjectData, Func<IObjectData, T> creator, Func<T, IObjectDataValueFactory, bool, IObjectData> formatter, T[] sampleValues) : base(createEmptyObjectData, sampleValues) {
				// argument checks
				if (creator == null) {
					throw new ArgumentNullException(nameof(creator));
				}
				if (formatter == null) {
					throw new ArgumentNullException(nameof(formatter));
				}

				// initialize members
				this.Creator = creator;
				this.Formatter = formatter;
			}

			#endregion


			#region overrides

			protected override T GetValueAdapter(IObjectData data, string name, T defaultValue) {
				return data.GetObjectValue(name, defaultValue, this.Creator);
			}

			protected override void SetValueAdapter(IObjectData data, string name, T value, bool omitDefault, bool isDefault) {
				data.SetObjectValue(name, value, this.Formatter, omitDefault, isDefault);
			}

			protected override Tuple<T> GetValueAdapter(IObjectData data, string name) {
				return data.GetObjectValue(name, this.Creator);
			}

			protected override void SetValueAdapter(IObjectData data, string name, Tuple<T> value) {
				data.SetObjectValue(name, value, this.Formatter);
			}

			#endregion
		}



		public abstract class DateTimeValueTestBase: ObjectDataExtensionTestBase.ValueTypeObjectValueTestBase<DateTime> {
			#region data

			public const string Year = "Year";

			public const string Month = "Month";

			public const string Day = "Day";

			public static readonly DateTime[] DefaultSampleValues = new DateTime[] {
				new DateTime(2000, 1, 1),
				new DateTime(2017, 3, 3),
				new DateTime(2005, 12, 31),
				new DateTime(2010, 8, 20),
				new DateTime(2020, 4, 30)
			};

			#endregion


			#region creation and disposal

			protected DateTimeValueTestBase(Func<IObjectData> createEmptyObjectData, DateTime[] sampleValues) : base(createEmptyObjectData, Create, Format, sampleValues) {
			}

			protected DateTimeValueTestBase(Func<IObjectData> createEmptyObjectData) : this(createEmptyObjectData, DefaultSampleValues) {
			}

			#endregion


			#region methods

			public static DateTime Create(IObjectData data) {
				// argument checks
				if (data == null) {
					throw new FormatException("A null cannot be converted to DateTime.");
				}

				int year = data.GetInt32Value(Year, 0);
				int month = data.GetInt32Value(Month, 0);
				int day = data.GetInt32Value(Day, 0);
				return new DateTime(year, month, day);
			}

			public static IObjectData Format(DateTime value, IObjectDataValueFactory factory, bool omitDefault) {
				// argument checks
				if (value == null) {
					return null;
				}
				Debug.Assert(factory != null);

				IObjectData data = factory.CreateEmptyObjectData();
				data.SetInt32Value(Year, value.Year, omitDefault, value.Year == 0);
				data.SetInt32Value(Month, value.Month, omitDefault, value.Month == 0);
				data.SetInt32Value(Day, value.Day, omitDefault, value.Day == 0);
				return data;
			}

			#endregion
		}

		public abstract class VersionValueTestBase: ObjectDataExtensionTestBase.ReferenceTypeObjectValueTestBase<Version> {
			#region data

			public const string Major = "Major";

			public const string Minor = "Minor";

			public static readonly Version[] DefaultSampleValues = new Version[] {
				new Version(1, 0),
				new Version(3, 0),
				new Version(1, 1),
				new Version(1, 2)
			};

			#endregion


			#region creation and disposal

			protected VersionValueTestBase(Func<IObjectData> createEmptyObjectData, Version[] sampleValues) : base(createEmptyObjectData, Create, Format, sampleValues) {
			}

			protected VersionValueTestBase(Func<IObjectData> createEmptyObjectData) : this(createEmptyObjectData, DefaultSampleValues) {
			}

			#endregion


			#region methods

			public static Version Create(IObjectData data) {
				// argument checks
				if (data == null) {
					return null;
				}

				int major = data.GetInt32Value(Major, 0);
				int minor = data.GetInt32Value(Minor, 0);
				return new Version(major, minor);
			}

			public static IObjectData Format(Version value, IObjectDataValueFactory factory, bool omitDefault) {
				// argument checks
				if (value == null) {
					return null;
				}
				Debug.Assert(factory != null);

				IObjectData data = factory.CreateEmptyObjectData();
				data.SetInt32Value(Major, value.Major, omitDefault, value.Major == 0);
				data.SetInt32Value(Minor, value.Minor, omitDefault, value.Minor == 0);
				return data;
			}

			#endregion
		}

		#endregion
#endif
	}
}
