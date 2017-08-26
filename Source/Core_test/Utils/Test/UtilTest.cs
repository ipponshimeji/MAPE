using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Xunit;


namespace MAPE.Utils.Test {
	public class UtilTest {
		#region test - ParseEndPoint

		public class ParseEndPoint {
			#region tests

			[Fact(DisplayName = "s: DNS name with port")]
			public void Args_s_DNSwithPort() {
				// ARRANGE
				string s = "www.example.org:123";
				bool canOmitPort = false;

				// ACT
				DnsEndPoint actual = Util.ParseEndPoint(s, canOmitPort);

				// ASSERT
				Assert.Equal("www.example.org", actual.Host);
				Assert.Equal(123, actual.Port);
				Assert.Equal(AddressFamily.Unspecified, actual.AddressFamily);
			}

			[Fact(DisplayName = "s: DNS name without port")]
			public void Args_s_DNSwithoutPort() {
				// ARRANGE
				string s = "www.example.org";
				bool canOmitPort = false;

				// ACT, ASSERT
				Assert.Throws<FormatException>(
					() => {
						DnsEndPoint actual = Util.ParseEndPoint(s, canOmitPort);
					}
				);
			}

			[Fact(DisplayName = "s: IPv4 with port")]
			public void Args_s_IPv4withPort() {
				// ARRANGE
				string s = "192.168.0.7:456";
				bool canOmitPort = false;

				// ACT
				DnsEndPoint actual = Util.ParseEndPoint(s, canOmitPort);

				// ASSERT
				Assert.Equal("192.168.0.7", actual.Host);
				Assert.Equal(456, actual.Port);
				Assert.Equal(AddressFamily.Unspecified, actual.AddressFamily);
			}

			[Fact(DisplayName = "s: IPv4 without port")]
			public void Args_s_IPv4withoutPort() {
				// ARRANGE
				string s = "192.168.0.128";
				bool canOmitPort = false;

				// ACT, ASSERT
				Assert.Throws<FormatException>(
					() => {
						DnsEndPoint actual = Util.ParseEndPoint(s, canOmitPort);
					}
				);
			}

			[Fact(DisplayName = "s: IPv6 with port")]
			public void Args_s_IPv6withPort() {
				// ARRANGE
				string s = "[FD00::5]:789";
				bool canOmitPort = false;

				// ACT
				DnsEndPoint actual = Util.ParseEndPoint(s, canOmitPort);

				// ASSERT
				Assert.Equal("[FD00::5]", actual.Host, ignoreCase: true);
				Assert.Equal(789, actual.Port);
				Assert.Equal(AddressFamily.Unspecified, actual.AddressFamily);
			}

			[Fact(DisplayName = "s: IPv6 without port")]
			public void Args_s_IPv6withoutPort() {
				// ARRANGE
				string s = "[FD00::5]";
				bool canOmitPort = false;

				// ACT, ASSERT
				Assert.Throws<FormatException>(
					() => {
						DnsEndPoint actual = Util.ParseEndPoint(s, canOmitPort);
					}
				);
			}

			[Fact(DisplayName = "s: null")]
			public void Args_s_null() {
				// ARRANGE
				string s = null;
				bool canOmitPort = false;

				// ACT, ASSERT
				Assert.Throws<FormatException>(
					() => {
						DnsEndPoint actual = Util.ParseEndPoint(s, canOmitPort);
					}
				);
			}

			[Fact(DisplayName = "s: empty")]
			public void Args_s_empty() {
				// ARRANGE
				string s = string.Empty;
				bool canOmitPort = false;

				// ACT, ASSERT
				Assert.Throws<FormatException>(
					() => {
						DnsEndPoint actual = Util.ParseEndPoint(s, canOmitPort);
					}
				);
			}

			[Fact(DisplayName = "s: invalid")]
			public void Args_s_invalid() {
				// ARRANGE
				string s = "....";
				bool canOmitPort = false;

				// ACT, ASSERT
				Assert.Throws<FormatException>(
					() => {
						DnsEndPoint actual = Util.ParseEndPoint(s, canOmitPort);
					}
				);
			}

			[Fact(DisplayName = "s: with path")]
			public void Args_s_withPath() {
				// ARRANGE
				string s = "www.example.org:80/path";
				bool canOmitPort = false;

				// ACT, ASSERT
				Assert.Throws<FormatException>(
					() => {
						DnsEndPoint actual = Util.ParseEndPoint(s, canOmitPort);
					}
				);
			}

			[Fact(DisplayName = "s: DNS name with port, canOmitPort: true")]
			public void Args_s_DNSwithPort_canOmitPort_true() {
				// ARRANGE
				string s = "www.example.org:1234";
				bool canOmitPort = true;

				// ACT
				DnsEndPoint actual = Util.ParseEndPoint(s, canOmitPort);

				// ASSERT
				Assert.Equal("www.example.org", actual.Host);
				Assert.Equal(1234, actual.Port);
				Assert.Equal(AddressFamily.Unspecified, actual.AddressFamily);
			}

			[Fact(DisplayName = "s: DNS name without port, canOmitPort: true")]
			public void Args_s_DNSwithoutPort_canOmitPort_true() {
				// ARRANGE
				string s = "www.example.org";
				bool canOmitPort = true;

				// ACT
				DnsEndPoint actual = Util.ParseEndPoint(s, canOmitPort);

				// ASSERT
				Assert.Equal("www.example.org", actual.Host);
				Assert.Equal(80, actual.Port);	// default value (80)
				Assert.Equal(AddressFamily.Unspecified, actual.AddressFamily);
			}

			#endregion
		}

		#endregion
	}
}
