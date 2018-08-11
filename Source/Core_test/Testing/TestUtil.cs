using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;


namespace MAPE.Testing {
	public static class TestUtil {
		#region data

		public static readonly IReadOnlyList<string> DefaultAdditionalHeaderNames = new string[] { "Date", "Server" }; 

		#endregion


		#region methods

		public static T Wait<T>(Task<T> task) {
			// argument checks
			if (task == null) {
				throw new ArgumentNullException(nameof(task));
			}

			// wait for completion of the task and return its result
			task.Wait();
			return task.Result;
		}

		public static FileStream CreateTempFileStream() {
			const int defaultBufferSize = 4096;     // same to the .NET Framework implementation
			string path = Path.GetTempFileName();
			try {
				return new FileStream(path, FileMode.Truncate, FileAccess.ReadWrite, FileShare.None, defaultBufferSize, FileOptions.DeleteOnClose);
			} catch {
				File.Delete(path);
				throw;
			}
		}

		public static int[] GetFreePortToListen(IPAddress address, int count) {
			// argument checks
			if (address == null) {
				throw new ArgumentNullException(nameof(address));
			}
			if (count <= 0) {
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			// try to listen with port 0, which make system find the free port to listen
			int[] ports = new int[count];
			TcpListener[] listeners = new TcpListener[count];
			int i = 0;
			try {
				for (i = 0; i < count; ++i) {
					TcpListener listener;
					listener = new TcpListener(address, 0);
					listeners[i] = listener;

					listener.Start();
					ports[i] = ((IPEndPoint)listener.LocalEndpoint).Port;
				}
			} finally {
				for (--i; 0 <= i; --i) {
					try {
						TcpListener listener = listeners[i];
						listener.Stop();
					} catch {
						// ignore error
					}
				}
			}

			return ports;
		}

		public static void AssertEqualResponse(HttpResponseMessage expected, HttpWebResponse actual, IEnumerable<string> additionalHeaderNames) {
			// argument checks
			if (expected == null) {
				Assert.Null(actual);
			}

			// ASSERT: HTTP version
			Assert.Equal(expected.Version, actual.ProtocolVersion);

			// ASSERT: status code
			Assert.Equal(expected.StatusCode, actual.StatusCode);

			// ASSERT: reason phrase
			Assert.Equal(expected.ReasonPhrase, actual.StatusDescription);

			// ASSERT: headers
			IDictionary<string, string> copyHeaders(WebHeaderCollection headers) {
				// argument checks
				Debug.Assert(headers != null);

				// copy the headers into the dictionary
				Dictionary<string, string> copy = new Dictionary<string, string>(headers.Count);
				foreach (string name in headers.Keys) {
					copy[name] = headers[name];
				}

				return copy;
			}
			void assertHeaders(HttpHeaders eHeaders, IDictionary<string, string> aHeaders) {
				foreach (var header in eHeaders) {
					// get header values
					string expectedValue = string.Join(", ", header.Value);
					string actualValue = aHeaders[header.Key]; // may be null

					// assert
					Assert.Equal(expectedValue, actualValue);

					// remove asserted header from the aHeaders
					aHeaders.Remove(header.Key);
				}
			}
			IDictionary<string, string> actualHeaders = copyHeaders(actual.Headers);
			assertHeaders(expected.Headers, actualHeaders);	// assert except content headers

			// ASSERT: content
			HttpContent content = expected.Content;
			if (content == null) {
				Assert.Equal(0, actual.ContentLength);
			} else {
				assertHeaders(content.Headers, actualHeaders);
				MediaTypeHeaderValue contentType = content.Headers.ContentType;
				if (contentType != null && contentType.MediaType.StartsWith("text/")) {
					AssertEqualStringContent(content, actual);
				} else {
					AssertEqualContent(content, actual);
				}
			}

			// ASSERT: remains of actualHeaders
			if (additionalHeaderNames != null) {
				foreach (string name in additionalHeaderNames) {
					actualHeaders.Remove(name);
				}
				if (0 < actualHeaders.Count) {
					string names = string.Join(", ", actualHeaders.Keys);
					throw new XunitException($"Unexpected header(s) in the actual: {names}");
				}
			}

			return;
		}

		public static void AssertEqualResponse(HttpResponseMessage expected, HttpWebResponse actual) {
			AssertEqualResponse(expected, actual, DefaultAdditionalHeaderNames);
		}

		public static void AssertEqualStringContent(HttpContent expected, HttpWebResponse actual) {
			// argument checks
			if (expected == null) {
				throw new ArgumentNullException(nameof(expected));
			}
			if (actual == null) {
				throw new ArgumentNullException(nameof(actual));
			}

			// ASSERT: Content-Length
			long contentLength = expected.Headers.ContentLength ?? 0;
			Assert.Equal(contentLength, actual.ContentLength);

			// get contents as string
			string expectedContent = Wait(expected.ReadAsStringAsync());
			string actualContent;
			// use the same encoding to expected's
			Encoding actualEncoding = Encoding.GetEncoding(expected.Headers.ContentType.CharSet);
			using (StreamReader reader = new StreamReader(actual.GetResponseStream(), actualEncoding)) {
				actualContent = reader.ReadToEnd();
			}

			// ASSERT: content
			Assert.Equal(expectedContent, actualContent);
		}

		public static void AssertEqualContent(HttpContent expected, HttpWebResponse actual) {
			// argument checks
			if (expected == null) {
				throw new ArgumentNullException(nameof(expected));
			}
			if (actual == null) {
				throw new ArgumentNullException(nameof(actual));
			}

			// ASSERT: Content-Length
			long contentLength = expected.Headers.ContentLength ?? 0;
			Assert.Equal(contentLength, actual.ContentLength);

			// content
			if (0 < contentLength) {
				int bufferLength = 128;
				byte[] expectedBuffer = new byte[bufferLength];
				byte[] actualBuffer = new byte[bufferLength];
				void fill(Stream stream, byte[] buf, int length) {
					// argument checks
					Debug.Assert(0 < length);
					Debug.Assert(length <= buf.Length);

					// fill the buffer from the stream
					int offset = 0;
					while (offset < length) {
						int readLen = stream.Read(buf, offset, length - offset);
						if (readLen <= 0) {
							throw new IOException("No enough data");
						}
						offset += readLen;
					}
					Debug.Assert(offset == length);
				}
				void assert(byte[] eBuf, byte[] aBuf, int length, long bIndex) {
					for(int i = 0; i < length; ++i) {
						if (eBuf[i] != aBuf[i]) {
							string format = "0x{0:X02} ({0})";
							string eLabel = string.Format(format, eBuf[i]);
							string aLabel = string.Format(format, aBuf[i]);
							long index = bIndex + i;
							throw new AssertActualExpectedException(eLabel, aLabel, $"Assert.Equal() Failure{Environment.NewLine}at index {index}");
						}
					}
				}

				long baseIndex = 0;
				using (Stream expectedStream = Wait(expected.ReadAsStreamAsync())) {
					using (Stream actualStream = actual.GetResponseStream()) {
						while (baseIndex < contentLength) {
							long remains = contentLength - baseIndex;
							int length = bufferLength;
							if (remains < bufferLength) {
								length = (int)remains;
							}
							fill(expectedStream, expectedBuffer, length);
							fill(actualStream, actualBuffer, length);
							// TODO: Should use Assert.Equal(IEnumerable, IEnumerable)? How tell baseIndex?
							assert(expectedBuffer, actualBuffer, length, baseIndex);
							baseIndex += length;
						}
					}
				}
			}
		}

		#endregion
	}
}
