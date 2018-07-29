using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Xunit;
using MAPE.Command.Settings;


namespace MAPE.Utils.Test {
	public class ActualProxySettingsTest {
		#region tests

		public class Constructor {
			#region tests

			[Fact(DisplayName = "default")]
			public void Default() {
				// ARRANGE

				// ACT
				ActualProxySettings target = new ActualProxySettings();

				// ASSERT
				Assert.Equal(ActualProxySettings.Defaults.Host, target.Host);
				Assert.Equal(ActualProxySettings.Defaults.Port, target.Port);
				Assert.Null(target.ConfigurationScript);
			}

			[Fact(DisplayName = "data: address")]
			public void AddressData() {
				// ARRANGE
				string host = "abc@example.org";
				int port = 80;
				string jsonText = $"{{\"Host\":\"{host}\", \"Port\": {port}}}";
				IObjectData data = new JsonObjectData(jsonText);

				// ACT
				ActualProxySettings target = new ActualProxySettings(data);

				// ASSERT
				Assert.Equal(host, target.Host);
				Assert.Equal(port, target.Port);
				Assert.Null(target.ConfigurationScript);
			}

			[Fact(DisplayName = "data: configuration script")]
			public void ConfigurationScriptData() {
				// ARRANGE
				string configurationScript = "file:///c:/test.pac";
				string jsonText = $"{{\"ConfigurationScript\":\"{configurationScript}\"}}";
				IObjectData data = new JsonObjectData(jsonText);

				// ACT
				ActualProxySettings target = new ActualProxySettings(data);

				// ASSERT
				Assert.Null(target.Host);
				Assert.Equal(ActualProxySettings.Defaults.Port, target.Port);
				Assert.Equal(configurationScript, target.ConfigurationScript);
			}

			[Fact(DisplayName = "data: empty")]
			public void EmptyData() {
				// ARRANGE
				string jsonText = $"{{}}";
				IObjectData data = new JsonObjectData(jsonText);

				// ACT
				ActualProxySettings target = new ActualProxySettings(data);

				// ASSERT
				Assert.Equal(ActualProxySettings.Defaults.Host, target.Host);
				Assert.Equal(ActualProxySettings.Defaults.Port, target.Port);
				Assert.Null(target.ConfigurationScript);
			}

			[Fact(DisplayName = "data: null host")]
			public void NullHostData() {
				// ARRANGE
				int port = 80;
				string jsonText = $"{{\"Host\":null, \"Port\": {port}}}";
				IObjectData data = new JsonObjectData(jsonText);

				// ACT, ASSERT
				Assert.Throws<FormatException>(() => {
					new ActualProxySettings(data);
				});
			}

			[Fact(DisplayName = "data: empty host")]
			public void EmptyHostData() {
				// ARRANGE
				int port = 80;
				string jsonText = $"{{\"Host\":\"\", \"Port\": {port}}}";
				IObjectData data = new JsonObjectData(jsonText);

				// ACT, ASSERT
				Assert.Throws<FormatException>(() => {
					new ActualProxySettings(data);
				});
			}

			#endregion
		}

		#endregion
	}
}
