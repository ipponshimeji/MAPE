using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Xunit;


namespace MAPE.Http.Test {
	public class ResponseTest: MessageTest {
		#region test - Read & Write

		public new abstract class ReadAndWriteTestBase<TResponse>: MessageTest.ReadAndWriteTestBase<TResponse> where TResponse: Response {
			#region creation

			protected ReadAndWriteTestBase(IAdapter adapter): base(adapter) {
			}

			#endregion
		}

		public abstract class ReadAndWriteBasicTest<TResponse>: ReadAndWriteTestBase<TResponse> where TResponse : Response {
			#region creation

			protected ReadAndWriteBasicTest(IAdapter adapter): base(adapter) {
			}

			#endregion


			#region tests

			[Fact(DisplayName = "simple")]
			public void Simple() {
				// ARRANGE
				string input = CreateMessageString(
					"HTTP/1.1 200 OK",
					"",
					EmptyBody
				);
				string expectedOutput = input;

				// Test
				TestReadWrite(input, expectedOutput, (response) => {
					Assert.Equal(new Version(1, 1), response.Version);
					Assert.Equal(200, response.StatusCode);
				});
			}

			[Fact(DisplayName = "HTTP-version: 1.0")]
			public void Version_10() {
				// ARRANGE
				string input = CreateMessageString(
					"HTTP/1.0 200 OK",
					"",
					EmptyBody
				);
				string expectedOutput = input;

				// Test
				TestReadWrite(input, expectedOutput, (response) => {
					Assert.Equal(new Version(1, 0), response.Version);
					Assert.Equal(200, response.StatusCode);
				});
			}

			[Fact(DisplayName = "HTTP-version: invalid HTTP-name")]
			public void Version_InvalidHTTPName() {
				// ARRANGE
				string input = CreateMessageString(
					"HTTPS/1.1 200 OK",
					"",
					EmptyBody
				);
				string expectedOutput = null;

				// Test
				HttpException exception = Assert.Throws<HttpException>(
					() => TestReadWrite(input, expectedOutput)
				);
				Assert.Equal(HttpStatusCode.BadGateway, exception.HttpStatusCode);
			}

			[Fact(DisplayName = "HTTP-version: lower-case HTTP-name")]
			public void Version_LowerCaseHTTPName() {
				// ARRANGE
				string input = CreateMessageString(
					"http/1.1 200 OK",
					"",
					EmptyBody
				);
				string expectedOutput = null;

				// Test
				HttpException exception = Assert.Throws<HttpException>(
					() => TestReadWrite(input, expectedOutput)
				);
				Assert.Equal(HttpStatusCode.BadGateway, exception.HttpStatusCode);
			}

			[Fact(DisplayName = "HTTP-version: invalid digits")]
			public void Version_InvalidDigits() {
				// ARRANGE
				string input = CreateMessageString(
					"HTTP/1.1.2 200 OK",
					"",
					EmptyBody
				);
				string expectedOutput = null;

				// Test
				HttpException exception = Assert.Throws<HttpException>(
					() => TestReadWrite(input, expectedOutput)
				);
				Assert.Equal(HttpStatusCode.BadGateway, exception.HttpStatusCode);
			}

			[Fact(DisplayName = "status-code: 407")]
			public void StatusCode_407() {
				// ARRANGE
				string input = CreateMessageString(
					"HTTP/1.1 407 Proxy Authentication Required",
					"",
					EmptyBody
				);
				string expectedOutput = input;

				// Test
				TestReadWrite(input, expectedOutput, (response) => {
					Assert.Equal(407, response.StatusCode);
				});
			}

			[Fact(DisplayName = "status-code: non-digit")]
			public void StatusCode_NonDigit() {
				// ARRANGE
				string input = CreateMessageString(
					"HTTP/1.1 XYZ OK",
					"",
					EmptyBody
				);
				string expectedOutput = null;

				// Test
				HttpException exception = Assert.Throws<HttpException>(
					() => TestReadWrite(input, expectedOutput)
				);
				Assert.Equal(HttpStatusCode.BadGateway, exception.HttpStatusCode);
			}

			[Fact(DisplayName = "status-code: too many digits")]
			public void StatusCode_TooManyDigits() {
				// ARRANGE
				string input = CreateMessageString(
					"HTTP/1.1 0200 OK",
					"",
					EmptyBody
				);
				string expectedOutput = null;

				// Test
				HttpException exception = Assert.Throws<HttpException>(
					() => TestReadWrite(input, expectedOutput)
				);
				Assert.Equal(HttpStatusCode.BadGateway, exception.HttpStatusCode);
			}

			[Fact(DisplayName = "status-code: too little digits")]
			public void StatusCode_TooLittleDigits() {
				// ARRANGE
				string input = CreateMessageString(
					"HTTP/1.1 20 Invalid Status Code",
					"",
					EmptyBody
				);
				string expectedOutput = null;

				// Test
				HttpException exception = Assert.Throws<HttpException>(
					() => TestReadWrite(input, expectedOutput)
				);
				Assert.Equal(HttpStatusCode.BadGateway, exception.HttpStatusCode);
			}

			[Fact(DisplayName = "reason-phrase: empty")]
			public void ReasonPhrase_Empty() {
				// ARRANGE
				string input = CreateMessageString(
					"HTTP/1.1 200 ",
					"",
					EmptyBody
				);
				string expectedOutput = input;

				// Test
				TestReadWrite(input, expectedOutput, (response) => {
					Assert.Equal(new Version(1, 1), response.Version);
					Assert.Equal(200, response.StatusCode);
				});
			}

			[Fact(DisplayName = "header: large")]
			public void Header_Large() {
				// ARRANGE
				StringBuilder buf = new StringBuilder();
				buf.Append($"HTTP/1.1 200 OK{CRLF}");
				for (int i = 0; i < 50; ++i) {
					buf.Append($"X-Test-{i}: 012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789{CRLF}");
				}
				buf.Append(CRLF);
				string input = buf.ToString();

				// This sample header to be tested must consume more than two memory blocks.
				// Note that the length of the string is equal to the count of message octets in this sample.  
				Debug.Assert(ComponentFactory.MemoryBlockCache.MemoryBlockSize < input.Length);
				string expectedOutput = input;

				// Test
				TestReadWrite(input, expectedOutput, (response) => {
					Assert.Equal(200, response.StatusCode);
				});
			}

			[Fact(DisplayName = "body: tiny")]
			public void Body_Tiny() {
				// ARRANGE
				string input = CreateMessageString(
					"HTTP/1.1 200 OK",
					"Content-Length: 1",
					"",
					"1"
				);
				// The body length should be in range of 'tiny' length.
				// That is, the whole message must be stored in one memory block.
				// In a Response object, 'tiny' length body is stored in the rest of header's memory block.
				// Note that the length of the string is equal to the count of message octets in this sample.  
				Debug.Assert(input.Length < ComponentFactory.MemoryBlockCache.MemoryBlockSize);
				string expectedOutput = input;

				// Test
				TestReadWrite(input, expectedOutput, (response) => {
					Assert.Equal(200, response.StatusCode);
				});
			}

			[Fact(DisplayName = "body: small")]
			public void Body_Small() {
				// ARRANGE
				const int bodyLength = ComponentFactory.MemoryBlockCache.MemoryBlockSize - 10;
				string header = CreateMessageString(
					"HTTP/1.1 200 OK",
					$"Content-Length: {bodyLength}",
					""
				);

				// The body length should be in range of 'small' length.
				// That is, 
				// * It cannot be stored in the rest of header's memory block, but
				// * It can be stored in one memory block.
				// In a Response object, 'small' length body is stored in a memory block.
				Debug.Assert(ComponentFactory.MemoryBlockCache.MemoryBlockSize < header.Length + 2 + bodyLength);
				Debug.Assert(bodyLength <= ComponentFactory.MemoryBlockCache.MemoryBlockSize);

				// Test
				TestReadWriteSimpleBody(header, bodyLength, (response) => {
					Assert.Equal(200, response.StatusCode);
				});
			}

			[Fact(DisplayName = "body: medium")]
			public void Body_Medium() {
				// ARRANGE
				const int bodyLength = 10 * 1024;   // 10k
				string header = CreateMessageString(
					"HTTP/1.1 200 OK",
					$"Content-Length: {bodyLength}",
					""
				);

				// The body length should be in range of 'medium' length.
				// That is, 
				// * It must be larger than the memory block size, and
				// * It must be smaller than or equal to BodyBuffer.BodyStreamThreshold.
				// In a Response object, 'medium' length body is stored in a MemoryStream.
				Debug.Assert(ComponentFactory.MemoryBlockCache.MemoryBlockSize < bodyLength);
				Debug.Assert(bodyLength <= BodyBuffer.BodyStreamThreshold);

				// Test
				TestReadWriteSimpleBody(header, bodyLength, (response) => {
					Assert.Equal(200, response.StatusCode);
				});
			}

			[Fact(DisplayName = "body: large")]
			public void Body_Large() {
				// ARRANGE
				const int bodyLength = BodyBuffer.BodyStreamThreshold * 2;
				string header = CreateMessageString(
					"HTTP/1.1 200 OK",
					$"Content-Length: {bodyLength}",
					""
				);

				// The body length should be in range of 'large' length.
				// That is, 
				// * It must be larger than BodyBuffer.BodyStreamThreshold.
				// In a Response object, 'large' length body is stored in a FileStream.
				Debug.Assert(BodyBuffer.BodyStreamThreshold < bodyLength);

				// Test
				TestReadWriteSimpleBody(header, bodyLength, (response) => {
					Assert.Equal(200, response.StatusCode);
				});
			}

			[Fact(DisplayName = "body: chunked")]
			public void Body_Chunked() {
				using (MemoryStream input = new MemoryStream()) {
					// ToDo: in VS2017, convert it to a local method
					Action<string> writeLine = (string line) => {
						WriteLinesTo(input, line);
					};
					Action<long> writeChunkData = (long size) => {
						WriteRandomBody(size, input, appendCRLF: true);
					};

					// write header
					WriteLinesTo(
						input,
						"HTTP/1.1 200 OK",
						$"Transfer-Encoding: chunked",
						""
					);

					// write chunked body
					writeLine("A0");        // chunk-size, in upper case
					writeChunkData(0xA0);   // chunk-data
					writeLine("cd");        // chunk-size, in lower case 
					writeChunkData(0xCD);   // chunk-data
					writeLine("0");         // last-chunk
					writeLine("");			// end of chunked-body

					input.Position = 0;
					Stream expectedOutput = input;  // same to the input

					// Test
					TestReadWrite(input, expectedOutput, (response) => {
						Assert.Equal(200, response.StatusCode);
					});
				}
			}

			[Fact(DisplayName = "body: chunked with trailers")]
			public void Body_Chunked_with_Trailers() {
				using (MemoryStream input = new MemoryStream()) {
					// ToDo: in VS2017, convert it to a local method
					Action<string> writeLine = (string line) => {
						WriteLinesTo(input, line);
					};
					Action<long> writeChunkData = (long size) => {
						WriteRandomBody(size, input, appendCRLF: true);
					};

					// write header
					WriteLinesTo(
						input,
						"HTTP/1.1 200 OK",
						$"Transfer-Encoding: chunked",
						""
					);

					// write chunked body
					writeLine("10");				// chunk-size
					writeChunkData(0x10);			// chunk-data
					writeLine("10");				// chunk-size
					writeChunkData(0x10);			// chunk-data
					writeLine("10");				// chunk-size
					writeChunkData(0x10);			// chunk-data
					writeLine("0");					// last-chunk
					writeLine("X-Test-1: dummy");	// trailer
					writeLine("X-Test-2: dummy");   // trailer
					writeLine("");					// end of chunked-body

					input.Position = 0;
					Stream expectedOutput = input;  // same to the input

					// Test
					TestReadWrite(input, expectedOutput, (response) => {
						Assert.Equal(200, response.StatusCode);
					});
				}
			}

			// ToDo: body
			//  with chunk-ext 
			//  multi transfer-coding in Transfer-Encoding

			#endregion
		}

		public class ReadAndWrite: ReadAndWriteBasicTest<Response> {
			#region types

			private class ResponseAdapter: IAdapter {
				#region properties

				public static readonly ResponseAdapter Instance = new ResponseAdapter();

				#endregion


				#region IAdapter

				public Response Create() {
					return new Response();
				}

				public bool Read(Response message, Request request) {
					return message.Read(request);
				}

				public void Write(Response message) {
					message.Write();
				}

				#endregion
			}

			#endregion


			#region creation

			public ReadAndWrite(): base(ResponseAdapter.Instance) {
			}

			#endregion
		}

		#endregion
	}
}
