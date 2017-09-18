using System;
using System.Diagnostics;
using System.IO;
using System.Net;
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

		public abstract class ReadAndWriteBasicTest<TResponse>: ReadAndWriteTestBase<TResponse> where TResponse: Response {
			#region creation

			protected ReadAndWriteBasicTest(IAdapter adapter): base(adapter) {
			}

			#endregion


			#region tests

			[Fact(DisplayName = "simple")]
			public void Simple() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						Assert.Equal(new Version(1, 1), response.Version);
						Assert.Equal(200, response.StatusCode);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "HTTP-version: 1.0")]
			public void Version_10() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.0 200 OK",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						Assert.Equal(new Version(1, 0), response.Version);
						Assert.Equal(200, response.StatusCode);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "HTTP-version: invalid HTTP-name")]
			public void Version_InvalidHTTPName() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTPS/1.1 200 OK", // HTTP-version: invalid! 
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					HttpException exception = Assert.Throws<HttpException>(
						() => TestReadAndWrite(sample)
					);
					Assert.Equal(HttpStatusCode.BadGateway, exception.HttpStatusCode);
				}
			}

			[Fact(DisplayName = "HTTP-version: lower-case HTTP-name")]
			public void Version_LowerCaseHTTPName() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"http/1.1 200 OK",  //  HTTP-version: invalid! 
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					HttpException exception = Assert.Throws<HttpException>(
						() => TestReadAndWrite(sample)
					);
					Assert.Equal(HttpStatusCode.BadGateway, exception.HttpStatusCode);
				}
			}

			[Fact(DisplayName = "HTTP-version: invalid digits")]
			public void Version_InvalidDigits() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1.2 200 OK",    // HTTP-version: invalid! 
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					HttpException exception = Assert.Throws<HttpException>(
						() => TestReadAndWrite(sample)
					);
					Assert.Equal(HttpStatusCode.BadGateway, exception.HttpStatusCode);
				}
			}

			[Fact(DisplayName = "status-code: 407")]
			public void StatusCode_407() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 407 Proxy Authentication Required",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						Assert.Equal(407, response.StatusCode);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "status-code: non-digit")]
			public void StatusCode_NonDigit() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 XYZ OK",  // status-code: non-digit!
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					HttpException exception = Assert.Throws<HttpException>(
						() => TestReadAndWrite(sample)
					);
					Assert.Equal(HttpStatusCode.BadGateway, exception.HttpStatusCode);
				}
			}

			[Fact(DisplayName = "status-code: too long digits")]
			public void StatusCode_TooLongDigits() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 0200 OK",	// status-code: too long digits!
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					HttpException exception = Assert.Throws<HttpException>(
						() => TestReadAndWrite(sample)
					);
					Assert.Equal(HttpStatusCode.BadGateway, exception.HttpStatusCode);
				}
			}

			[Fact(DisplayName = "status-code: too short digits")]
			public void StatusCode_TooShortDigits() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 20 Invalid Status Code",	// status-code: too short digits!
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					HttpException exception = Assert.Throws<HttpException>(
						() => TestReadAndWrite(sample)
					);
					Assert.Equal(HttpStatusCode.BadGateway, exception.HttpStatusCode);
				}
			}

			[Fact(DisplayName = "reason-phrase: empty")]
			public void ReasonPhrase_Empty() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 200 ",	// reason-phrase: empty
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						Assert.Equal(new Version(1, 1), response.Version);
						Assert.Equal(200, response.StatusCode);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "header: large")]
			public void Header_Large() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 200 OK"
					);
					for (int i = 0; i < 50; ++i) {
						sample.AppendText($"X-Test-{i}: 012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789", appendCRLF: true);
					}
					sample.AppendHeader(
						""
					);
					// This sample header to be tested must consume more than two memory blocks.
					Debug.Assert(ComponentFactory.MemoryBlockCache.MemoryBlockSize < sample.SampleWriterPosition);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						Assert.Equal(200, response.StatusCode);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "body: tiny")]
			public void Body_Tiny() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						"Content-Length: 1",
						""
					);
					sample.AppendBody("1");

					// The body length should be in range of 'tiny' length.
					// That is, the whole message must be stored in one memory block.
					// In a Response object, 'tiny' length body is stored in the rest of header's memory block.
					Debug.Assert(sample.SampleWriterPosition < ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						Assert.Equal(1, response.ContentLength);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "body: small")]
			public void Body_Small() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					const long bodyLength = ComponentFactory.MemoryBlockCache.MemoryBlockSize - 10;
					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						$"Content-Length: {bodyLength}",
						""
					);
					sample.AppendRandomData(bodyLength);

					// The body length should be in range of 'small' length.
					// That is, 
					// * It cannot be stored in the rest of header's memory block, but
					// * It can be stored in one memory block.
					// In a Response object, 'small' length body is stored in a memory block.
					Debug.Assert(ComponentFactory.MemoryBlockCache.MemoryBlockSize < sample.SampleWriterPosition);
					Debug.Assert(bodyLength <= ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						Assert.Equal(bodyLength, response.ContentLength);
					});
					sample.AssertOutputEqualToSample();
					Assert.Equal(1, actualMessageCount);
				}
			}

			[Fact(DisplayName = "body: medium")]
			public void Body_Medium() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					const long bodyLength = 10 * 1024;   // 10k
					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						$"Content-Length: {bodyLength}",
						""
					);
					sample.AppendRandomData(bodyLength);

					// The body length should be in range of 'medium' length.
					// That is, 
					// * It must be larger than the memory block size, and
					// * It must be smaller than or equal to BodyBuffer.BodyStreamThreshold.
					// In a Response object, 'medium' length body is stored in a MemoryStream.
					Debug.Assert(ComponentFactory.MemoryBlockCache.MemoryBlockSize < bodyLength);
					Debug.Assert(bodyLength <= BodyBuffer.BodyStreamThreshold);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						Assert.Equal(bodyLength, response.ContentLength);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "body: large")]
			public void Body_Large() {
				using (MessageSample sample = CreateMessageSample(largeMessage: true)) {
					// ARRANGE
					const long bodyLength = BodyBuffer.BodyStreamThreshold * 2;
					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						$"Content-Length: {bodyLength}",
						""
					);
					sample.AppendRandomData(bodyLength);

					// The body length should be in range of 'large' length.
					// That is, 
					// * It must be larger than BodyBuffer.BodyStreamThreshold.
					// In a Response object, 'large' length body is stored in a FileStream.
					Debug.Assert(BodyBuffer.BodyStreamThreshold < bodyLength);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						Assert.Equal(bodyLength, response.ContentLength);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "body: chunked")]
			public void Body_Chunked() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE

					// The sample.CheckChunkFlushing is not meaningful with Write() method,
					// because Write() writes stored body at once.
					Debug.Assert(sample.CheckChunkFlushing == false);

					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						"Transfer-Encoding: chunked",
						""
					);

					// write chunked body
					sample.AppendSimpleChunk(0xA0);
					sample.AppendSimpleChunk(0xCD);
					sample.AppendLastChunk();

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						Assert.Equal(-1, response.ContentLength);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "body: chunked with trailers")]
			public void Body_Chunked_with_Trailers() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE

					// The sample.CheckChunkFlushing is not meaningful with Write() method,
					// because Write() writes stored body at once.
					Debug.Assert(sample.CheckChunkFlushing == false);

					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						"Transfer-Encoding: chunked",
						""
					);

					// write chunked body
					sample.AppendSimpleChunk(0x10);
					sample.AppendSimpleChunk(0x10);
					sample.AppendSimpleChunk(0x10);
					sample.AppendLastChunk("X-Test-1: dummy");

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						Assert.Equal(-1, response.ContentLength);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "body: chunked large")]
			public void Body_Chunked_Large() {
				using (MessageSample sample = CreateMessageSample(largeMessage: true)) {
					// ARRANGE

					// The sample.CheckChunkFlushing is not meaningful with Write() method,
					// because Write() writes stored body at once.
					Debug.Assert(sample.CheckChunkFlushing == false);

					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						"Transfer-Encoding: chunked",
						""
					);

					// write chunked body
					for (int i = 0; i < 1024; ++i) {
						sample.AppendSimpleChunk(1024);
					}
					sample.AppendLastChunk();

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						Assert.Equal(-1, response.ContentLength);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "redirect: tiny")]
			public void Redirect_Tiny() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						"Content-Length: 10",
						""
					);
					sample.AppendBody("0123456789");

					// The body length should be in range of 'tiny' length.
					// That is, the whole message must be stored in one memory block.
					// In a Response object, 'tiny' length body is stored in the rest of header's memory block.
					Debug.Assert(sample.SampleWriterPosition < ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					// ACT & ASSERT
					int actualMessageCount = TestReadHeaderAndRedirect(sample, (response) => {
						Assert.Equal(10, response.ContentLength);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "redirect: medium")]
			public void Redirect_Medium() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					const long bodyLength = 10 * 1024;   // 10k
					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						$"Content-Length: {bodyLength}",
						""
					);
					sample.AppendRandomData(bodyLength);

					// The body length should be in range of 'medium' length.
					// That is, 
					// * It must be larger than the memory block size, and
					// * It must be smaller than or equal to BodyBuffer.BodyStreamThreshold.
					// In a Response object, 'medium' length body is stored in a MemoryStream.
					Debug.Assert(ComponentFactory.MemoryBlockCache.MemoryBlockSize < bodyLength);
					Debug.Assert(bodyLength <= BodyBuffer.BodyStreamThreshold);

					// ACT & ASSERT
					int actualMessageCount = TestReadHeaderAndRedirect(sample, (response) => {
						Assert.Equal(bodyLength, response.ContentLength);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "redirect: chunked")]
			public void Redirect_Chunked() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE

					// Redirect() supports chunked transfer
					sample.CheckChunkFlushing = true;

					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						"Transfer-Encoding: chunked",
						""
					);

					// write chunked body
					sample.AppendSimpleChunk(0x100);
					sample.AppendSimpleChunk(0x100);
					sample.AppendSimpleChunk(0x100);
					sample.AppendLastChunk();

					// ACT & ASSERT
					int actualMessageCount = TestReadHeaderAndRedirect(sample, (response) => {
						Assert.Equal(-1, response.ContentLength);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "skip body")]
			public void SkipBody() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						"Content-Length: 7",
						""
					);
					sample.AppendBody("ABCDEFG");

					// ACT & ASSERT
					int actualMessageCount = TestReadHeaderAndSkipBody(sample, (response) => {
						Assert.Equal(7, response.ContentLength);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertAllSampleBytesRead();
				}
			}

			[Fact(DisplayName = "prefetched bytes: Read/Write")]
			public void PrefetchedBytes_ReadAndWrite() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						"Content-Length: 7",
						""
					);
					sample.AppendBody("1234567");

					// insert 0-length body message in the middle of sequence
					// In prefetched bytes processing, 0-length body handling is mistakable.
					sample.AppendHeader(
						"HTTP/1.1 302 Found",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					sample.AppendHeader(
						"HTTP/1.1 404 Not Found",
						"Content-Length: 0",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// The body length should be in range of 'tiny' length.
					// "prefetched bytes" occurs when the body is 'tiny' length for simple body.
					// In this case, whole three messages are on one memory block. 
					Debug.Assert(sample.SampleWriterPosition < ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					// ACT & ASSERT
					int counter = 0;
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						switch (counter) {
							case 0:
								Assert.Equal(200, response.StatusCode);
								Assert.Equal(7, response.ContentLength);
								break;
							case 1:
								Assert.Equal(302, response.StatusCode);
								Assert.Equal(0, response.ContentLength);
								break;
							case 2:
								Assert.Equal(404, response.StatusCode);
								Assert.Equal(0, response.ContentLength);
								break;
						}
						++counter;
					});
					Assert.Equal(3, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "prefetched bytes: ReadHeader/Redirect")]
			public void PrefetchedBytes_ReadHeaderAndRedirect() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						"Content-Length: 7",
						""
					);
					sample.AppendBody("1234567");

					// insert 0-length body message in the middle of sequence
					// In prefetched bytes processing, 0-length body handling is mistakable.
					sample.AppendHeader(
						"HTTP/1.1 302 Found",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					sample.AppendHeader(
						"HTTP/1.1 404 Not Found",
						"Content-Length: 0",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// The body length should be in range of 'tiny' length.
					// "prefetched bytes" occurs when the body is 'tiny' length for simple body.
					// In this case, whole three messages are on one memory block. 
					Debug.Assert(sample.SampleWriterPosition < ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					// ACT & ASSERT
					int counter = 0;
					int actualMessageCount = TestReadHeaderAndRedirect(sample, (response) => {
						switch (counter) {
							case 0:
								Assert.Equal(200, response.StatusCode);
								Assert.Equal(7, response.ContentLength);
								break;
							case 1:
								Assert.Equal(302, response.StatusCode);
								Assert.Equal(0, response.ContentLength);
								break;
							case 2:
								Assert.Equal(404, response.StatusCode);
								Assert.Equal(0, response.ContentLength);
								break;
						}
						++counter;
					});
					Assert.Equal(3, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "prefetched bytes: ReadHeader/SkipBody")]
			public void PrefetchedBytes_ReadHeaderAndSkipBody() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						"Content-Length: 7",
						""
					);
					sample.AppendBody("1234567");

					// insert 0-length body message in the middle of sequence
					// In prefetched bytes processing, 0-length body handling is mistakable.
					sample.AppendHeader(
						"HTTP/1.1 302 Found",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					sample.AppendHeader(
						"HTTP/1.1 404 Not Found",
						"Content-Length: 0",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// The body length should be in range of 'tiny' length.
					// "prefetched bytes" occurs when the body is 'tiny' length for simple body.
					// In this case, whole three messages are on one memory block. 
					Debug.Assert(sample.SampleWriterPosition < ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					// ACT & ASSERT
					int counter = 0;
					int actualMessageCount = TestReadHeaderAndSkipBody(sample, (response) => {
						switch (counter) {
							case 0:
								Assert.Equal(200, response.StatusCode);
								Assert.Equal(7, response.ContentLength);
								break;
							case 1:
								Assert.Equal(302, response.StatusCode);
								Assert.Equal(0, response.ContentLength);
								break;
							case 2:
								Assert.Equal(404, response.StatusCode);
								Assert.Equal(0, response.ContentLength);
								break;
						}
						++counter;
					});
					Assert.Equal(3, actualMessageCount);
					sample.AssertAllSampleBytesRead();
				}
			}

			[Fact(DisplayName = "prefetched bytes: chunked")]
			public void PrefetchedBytes_Chunked() {
				using (MessageSample sample = CreateMessageSample()) {
					// Redirect() supports chunked transfer
					sample.CheckChunkFlushing = true;

					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						"Transfer-Encoding: chunked",
						""
					);
					sample.AppendSimpleChunk(0x10);
					sample.AppendSimpleChunk(0x10);
					sample.AppendLastChunk();

					// the whole first message is stored on the memory block in headerBuffer. 
					long firstMessageLen = sample.SampleWriterPosition;
					Debug.Assert(firstMessageLen < ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					// the second message uses the memory block in bodyBuffer. 
					sample.AppendHeader(
						"HTTP/1.1 302 Found",
						"Transfer-Encoding: chunked",
						""
					);
					long secondMessageBodyOffset = sample.SampleWriterPosition;
					sample.AppendSimpleChunk(1050);
					sample.AppendSimpleChunk(1050);	
					sample.AppendLastChunk();

					// the second message uses the memory block in bodyBuffer. 
					long secondMessageBodyLen = sample.SampleWriterPosition - secondMessageBodyOffset;
					Debug.Assert(ComponentFactory.MemoryBlockCache.MemoryBlockSize < secondMessageBodyLen);
					Debug.Assert(secondMessageBodyLen < 2 * ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					sample.AppendHeader(
						"HTTP/1.1 404 Not Found",
						"Transfer-Encoding: chunked",
						""
					);
					sample.AppendSimpleChunk(0x10);
					sample.AppendLastChunk();

					// ACT & ASSERT
					int counter = 0;
					int actualMessageCount = TestReadHeaderAndRedirect(sample, (response) => {
						switch (counter) {
							case 0:
								Assert.Equal(200, response.StatusCode);
								Assert.Equal(-1, response.ContentLength);
								break;
							case 1:
								Assert.Equal(302, response.StatusCode);
								Assert.Equal(-1, response.ContentLength);
								break;
							case 2:
								Assert.Equal(404, response.StatusCode);
								Assert.Equal(-1, response.ContentLength);
								break;
						}
						++counter;
					});
					Assert.Equal(3, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "prefetched bytes: InputReconnect")]
			public void PrefetchedBytes_InputReconnect() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"HTTP/1.1 200 OK",
						"Content-Length: 7",
						""
					);
					sample.AppendBody("1234567");

					sample.AppendHeader(
						"HTTP/1.1 302 Found",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					long thirdMessageOffset = sample.SampleWriterPosition;
					sample.AppendHeader(
						"HTTP/1.1 404 Not Found",
						"Content-Length: 0",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// The body length should be in range of 'tiny' length.
					// "prefetched bytes" occurs when the body is 'tiny' length for simple body.
					// In this case, whole three messages are on one memory block. 
					Debug.Assert(sample.SampleWriterPosition < ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					// the second message will be skipped
					string expectedOutput = string.Join(
						MessageSample.CRLF,   // separator
						"HTTP/1.1 200 OK",
						"Content-Length: 7",
						"",
						"1234567HTTP/1.1 404 Not Found",  // Note no CRLF at the end of the body
						"Content-Length: 0",
						"",
						MessageSample.EmptyBody
					);

					// ACT & ASSERT
					int counter = 0;
					int actualMessageCount = TestReadAndWrite(sample, (response) => {
						switch (counter) {
							case 0:
								Assert.Equal(200, response.StatusCode);
								Assert.Equal(7, response.ContentLength);
								// move to the third message
								// It causes InputReconnected event.
								sample.ChangeSampleReaderPosition(thirdMessageOffset);
								break;
							case 1:
								// the third message instead of the second message
								Assert.Equal(404, response.StatusCode);
								Assert.Equal(0, response.ContentLength);
								break;
						}
						++counter;
					});
					Assert.Equal(2, actualMessageCount);
					sample.AssertOutputEqualTo(expectedOutput);
				}
			}

			// ToDo: body
			//  with chunk-ext 
			//  multi transfer-coding in Transfer-Encoding
			// ToDo: request param

			#endregion
		}

		public class ReadAndWrite: ReadAndWriteBasicTest<Response> {
			#region types

			private class ResponseAdapter: IAdapter {
				#region properties

				public static readonly ResponseAdapter Instance = new ResponseAdapter();

				#endregion


				#region IAdapter

				public Response Create(IMessageIO io) {
					Response response = new Response();
					response.AttachIO(io);
					return response;
				}

				public bool Read(Response message, Request request) {
					return message.Read(request);
				}

				public void Write(Response message, bool suppressModification) {
					message.Write(suppressModification);
				}

				public bool ReadHeader(Response message, Request request) {
					return message.ReadHeader(request);
				}

				public void SkipBody(Response message) {
					message.SkipBody();
				}

				public void Redirect(Response message, bool suppressModification) {
					message.Redirect(suppressModification);
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
