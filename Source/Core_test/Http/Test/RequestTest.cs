using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using Xunit;


namespace MAPE.Http.Test {
	public class RequestTest: MessageTest {
		#region test - Read & Write (ReadHeader & Redirect)

		public new abstract class ReadAndWriteTestBase<TRequest>: MessageTest.ReadAndWriteTestBase<TRequest> where TRequest: Request {
			#region creation

			protected ReadAndWriteTestBase(IAdapter adapter): base(adapter) {
			}

			#endregion
		}

		public abstract class ReadAndWriteBasicTest<TRequest>: ReadAndWriteTestBase<TRequest> where TRequest : Request {
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
						"GET / HTTP/1.1",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Assert.Equal("GET", request.Method);
						Assert.Equal(new Version(1, 1), request.Version);
						Assert.Equal("www.example.org:80", request.Host);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "method: POST")]
			public void Method_POST() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"POST / HTTP/1.1",
						"Host: www.example.org",
						"Content-Length: 3",
						""
					);
					sample.AppendBody("123");

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Assert.Equal("POST", request.Method);
						Assert.Equal("www.example.org:80", request.Host);
						Assert.Equal(3, request.ContentLength);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "method: CONNECT")]
			public void Method_CONNECT() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"CONNECT www.example.org:443 HTTP/1.1",
						"Host: www.example.org:443",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Assert.Equal("CONNECT", request.Method);
						Assert.Equal(true, request.IsConnectMethod);
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
						"GET / HTTP/1.0",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Assert.Equal("GET", request.Method);
						Assert.Equal(new Version(1, 0), request.Version);
						Assert.Equal("www.example.org:80", request.Host);
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
						"GET / HTTPS/1.1",  // HTTP-version: invalid! 
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					HttpException actual = Assert.Throws<HttpException>(
						() => TestReadAndWrite(sample)
					);
					Assert.Equal(HttpStatusCode.BadRequest, actual.HttpStatusCode);
				}
			}

			[Fact(DisplayName = "HTTP-version: lower-case HTTP-name")]
			public void Version_LowerCaseHTTPName() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"GET / http/1.1",   // HTTP-version: invalid! 
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					HttpException actual = Assert.Throws<HttpException>(
						() => TestReadAndWrite(sample)
					);
					Assert.Equal(HttpStatusCode.BadRequest, actual.HttpStatusCode);
				}
			}

			[Fact(DisplayName = "HTTP-version: invalid digits")]
			public void Version_InvalidDigits() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"GET / HTTP/1.1.2", // HTTP-version: invalid! 
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					HttpException actual = Assert.Throws<HttpException>(
						() => TestReadAndWrite(sample)
					);
					Assert.Equal(HttpStatusCode.BadRequest, actual.HttpStatusCode);
				}
			}

			[Fact(DisplayName = "request-target: origin-form")]
			public void RequestTarget_OriginForm() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"GET /abc/def?ghi=kl HTTP/1.1",	// origin-form
						"Host: www.example.org:81",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Assert.Equal(new DnsEndPoint("www.example.org", 81), request.HostEndPoint);
						Assert.Equal("www.example.org:81", request.Host);
						Assert.Equal(new Span(30, 56), request.HostSpan);

						// TargetUri is null for request-target of origin-form
						Assert.Equal(null, request.TargetUri);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "request-target: absolute-form")]
			public void RequestTarget_AbsoluteForm() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"GET http://www.example.org/abc/def?ghij=kl HTTP/1.1",
						"Host: test.example.org:123",	// dare to give different value from request-target for test
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						// HostEndPoint and Host are derived from the request-target. 
						Assert.Equal(new DnsEndPoint("www.example.org", 80), request.HostEndPoint);
						Assert.Equal("www.example.org:80", request.Host);

						// HostSpan is the span of the actual Host field.
						Assert.Equal(new Span(53, 81), request.HostSpan);

						Assert.Equal(new Uri("http://www.example.org/abc/def?ghij=kl"), request.TargetUri);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "request-target: authority-form with port")]
			public void RequestTarget_AuthorityForm_with_port() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"CONNECT www.example.org:443 HTTP/1.1",
						"Host: www.example.org:400",	// dare to give different value from request-target for test
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						// HostEndPoint and Host are derived from the request-target. 
						Assert.Equal(new DnsEndPoint("www.example.org", 443), request.HostEndPoint);
						Assert.Equal("www.example.org:443", request.Host);

						// HostSpan is the span of the actual Host field.
						Assert.Equal(new Span(38, 65), request.HostSpan);

						// TargetUri is null for request-target of authority-form
						Assert.Equal(null, request.TargetUri);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "request-target: authority-form without port")]
			public void RequestTarget_AuthorityForm_without_port() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"CONNECT www.example.org HTTP/1.1",
						"Host: www.example.org:400",    // dare to give different value from request-target for test
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						// HostEndPoint and Host are derived from the request-target. 
						Assert.Equal(new DnsEndPoint("www.example.org", 443), request.HostEndPoint);
						Assert.Equal("www.example.org:443", request.Host);

						// HostSpan is the span of the actual Host field.
						Assert.Equal(new Span(34, 61), request.HostSpan);

						// TargetUri is null for request-target of authority-form
						Assert.Equal(null, request.TargetUri);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "request-target: asterisk-form")]
			public void RequestTarget_AsteriskForm() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"OPTIONS * HTTP/1.1",
						"Host: www.example.org:80",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Assert.Equal(new DnsEndPoint("www.example.org", 80), request.HostEndPoint);
						Assert.Equal("www.example.org:80", request.Host);
						Assert.Equal(new Span(20, 46), request.HostSpan);

						// TargetUri is null for request-target of asterisk-form
						Assert.Equal(null, request.TargetUri);
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
						"GET / HTTP/1.1",
						"Host: www.example.org:80"
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
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Assert.Equal("www.example.org:80", request.Host);
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
						"PUT / HTTP/1.1",
						"Host: www.example.org:80",
						"Content-Length: 5",
						""
					);
					sample.AppendBody("12345");

					// The body length should be in range of 'tiny' length.
					// That is, the whole message must be stored in one memory block.
					// In a Request object, 'tiny' length body is stored in the rest of header's memory block.
					Debug.Assert(sample.SampleWriterPosition < ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Assert.Equal(5, request.ContentLength);
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
						"PUT / HTTP/1.1",
						"Host: www.example.org:80",
						$"Content-Length: {bodyLength}",
						""
					);
					sample.AppendRandomData(bodyLength);

					// The body length should be in range of 'small' length.
					// That is, 
					// * It cannot be stored in the rest of header's memory block, but
					// * It can be stored in one memory block.
					// In a Request object, 'small' length body is stored in a memory block.
					Debug.Assert(ComponentFactory.MemoryBlockCache.MemoryBlockSize < sample.SampleWriterPosition);
					Debug.Assert(bodyLength <= ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Assert.Equal(bodyLength, request.ContentLength);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "body: medium")]
			public void Body_Medium() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					const long bodyLength = 10 * 1024;   // 10k
					sample.AppendHeader(
						"PUT / HTTP/1.1",
						"Host: www.example.org:80",
						$"Content-Length: {bodyLength}",
						""
					);
					sample.AppendRandomData(bodyLength);

					// The body length should be in range of 'medium' length.
					// That is, 
					// * It must be larger than the memory block size, and
					// * It must be smaller than or equal to BodyBuffer.BodyStreamThreshold.
					// In a Request object, 'medium' length body is stored in a MemoryStream.
					Debug.Assert(ComponentFactory.MemoryBlockCache.MemoryBlockSize < bodyLength);
					Debug.Assert(bodyLength <= BodyBuffer.BodyStreamThreshold);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Assert.Equal(bodyLength, request.ContentLength);
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
						"PUT / HTTP/1.1",
						"Host: www.example.org:80",
						$"Content-Length: {bodyLength}",
						""
					);
					sample.AppendRandomData(bodyLength);

					// The body length should be in range of 'large' length.
					// That is, 
					// * It must be larger than BodyBuffer.BodyStreamThreshold.
					// In a Request object, 'large' length body is stored in a FileStream.
					Debug.Assert(BodyBuffer.BodyStreamThreshold < bodyLength);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Assert.Equal(bodyLength, request.ContentLength);
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
						"PUT / HTTP/1.1",
						"Host: www.example.org:80",
						"Transfer-Encoding: chunked",
						""
					);

					// write chunked body
					sample.AppendChunkSizeLine("2F");   // chunk-size, simple
					sample.AppendRandomChunkData(0x2F); // chunk-data
					sample.AppendChunkSizeLine("08");   // chunk-size, in redundant format
					sample.AppendRandomChunkData(0x08); // chunk-data
					sample.AppendChunkSizeLine("000");  // last-chunk, in redundant format
					sample.AppendCRLF();

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Assert.Equal(-1, request.ContentLength);
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
						"PUT / HTTP/1.1",
						"Host: www.example.org:80",
						"Transfer-Encoding: chunked",
						""
					);

					// write chunked body with trailers
					sample.AppendLastChunk(
						"X-Test-1: dummy",
						"X-Test-2: dummy"
					);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Assert.Equal(-1, request.ContentLength);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			// ToDo: body
			//  with chunk-ext 
			//  multi transfer-coding in Transfer-Encoding
			// continuous read

			[Fact(DisplayName = "modification: append")]
			public void Modification_append() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"GET / HTTP/1.1",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					string expectedOutput = string.Join(
						MessageSample.CRLF,	// separator
						"GET / HTTP/1.1",
						"Host: www.example.org",
						"X-Test: dummy",
						"",
						MessageSample.EmptyBody
					);

					Func<Modifier, bool> handler = (modifier) => {
						modifier.WriteASCIIString("X-Test: dummy", appendCRLF: true);
						return true;
					};

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						// append a field
						request.AddModification(request.EndOfHeaderFields, handler);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualTo(expectedOutput);
				}
			}

			[Fact(DisplayName = "modification: append at the same point")]
			public void Modification_append_samepoint() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"GET / HTTP/1.1",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// keep the order of X-Test-1, X-Test-2 and X-Test-3.
					string expectedOutput = string.Join(
						MessageSample.CRLF,   // separator
						"GET / HTTP/1.1",
						"Host: www.example.org",
						"X-Test-1: dummy",
						"X-Test-2: dummy",
						"X-Test-3: dummy",
						"",
						MessageSample.EmptyBody
					);

					Func<Modifier, bool> handler1 = (modifier) => {
						modifier.WriteASCIIString("X-Test-1: dummy", appendCRLF: true);
						return true;
					};
					Func<Modifier, bool> handler2 = (modifier) => {
						modifier.WriteASCIIString("X-Test-2: dummy", appendCRLF: true);
						return true;
					};
					Func<Modifier, bool> handler3 = (modifier) => {
						modifier.WriteASCIIString("X-Test-3: dummy", appendCRLF: true);
						return true;
					};

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						Span span = request.EndOfHeaderFields;
						Debug.Assert(span.Length == 0);

						// add at the same point
						request.AddModification(span, handler1);
						request.AddModification(span, handler2);
						request.AddModification(span, handler3);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualTo(expectedOutput);
				}
			}

			[Fact(DisplayName = "modification: change")]
			public void Modification_change() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"GET http://www.example.org/test/index.html?abc=def HTTP/1.1",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					string expectedOutput = string.Join(
						MessageSample.CRLF,   // separator
						"GET /test/index.html?abc=def HTTP/1.1",
						"Host: www.example.org",
						"",
						MessageSample.EmptyBody
					);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						// change the existing field
						request.AddModification(
							request.RequestTargetSpan,
							(modifier) => {
								modifier.WriteASCIIString(request.TargetUri.PathAndQuery);
								return true;
							}
						);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualTo(expectedOutput);
				}
			}

			[Fact(DisplayName = "modification: remove")]
			public void Modification_remove() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"GET / HTTP/1.1",
						"Host: www.example.org",
						"Proxy-Authorization: dXNlcjpwYXNz",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					string expectedOutput = string.Join(
						MessageSample.CRLF,   // separator
						"GET / HTTP/1.1",
						"Host: www.example.org",
						"",
						MessageSample.EmptyBody
					);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						// remove the existing field
						request.AddModification(
							request.ProxyAuthorizationSpan,
							(modifier) => true		// remove
						);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualTo(expectedOutput);
				}
			}

			[Fact(DisplayName = "modification: cancel")]
			public void Modification_cancel() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"GET / HTTP/1.1",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						request.AddModification(
							request.HostSpan,
							(modifier) => false		// cancel modification!
						);
					});
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "modification: suppressModification")]
			public void Modification_suppressModification() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"GET http://www.example.org/test/index.html?abc=def HTTP/1.1",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// ACT & ASSERT
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						request.AddModification(
							request.RequestTargetSpan,
							(modifier) => {
								modifier.WriteASCIIString(request.TargetUri.PathAndQuery);
								return true;
							}
						);
					}, request: null, suppressModification: true);  // suppressModification!
					Assert.Equal(1, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "skip body")]
			public void SkipBody() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					const long bodyLength = 512;
					sample.AppendHeader(
						"PUT / HTTP/1.1",
						"Host: www.example.org:80",
						$"Content-Length: {bodyLength}",
						""
					);
					sample.AppendRandomData(bodyLength);

					// ACT & ASSERT
					int actualMessageCount = TestReadHeaderAndSkipBody(sample, (request) => {
						Assert.Equal(bodyLength, request.ContentLength);
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
						"GET / HTTP/1.1",
						"Host: www.example.org",
						"Content-Length: 3",
						""
					);
					sample.AppendBody("ABC");

					// insert 0-length body message in the middle of sequence
					// In prefetched bytes processing, 0-length body handling is mistakable.
					sample.AppendHeader(
						"HEAD / HTTP/1.1",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					sample.AppendHeader(
						"CONNECT www.example.org:443 HTTP/1.1",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// The body length should be in range of 'tiny' length.
					// "prefetched bytes" occurs when the body is 'tiny' length for simple body.
					// In this case, whole three messages are on one memory block. 
					Debug.Assert(sample.SampleWriterPosition < ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					// ACT & ASSERT
					int counter = 0;
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						switch (counter) {
							case 0:
								Assert.Equal("GET", request.Method);
								Assert.Equal(3, request.ContentLength);
								break;
							case 1:
								Assert.Equal("HEAD", request.Method);
								Assert.Equal(0, request.ContentLength);
								break;
							case 2:
								Assert.Equal("CONNECT", request.Method);
								Assert.Equal(0, request.ContentLength);
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
						"GET / HTTP/1.1",
						"Host: www.example.org",
						"Content-Length: 3",
						""
					);
					sample.AppendBody("ABC");

					// insert 0-length body message in the middle of sequence
					// In prefetched bytes processing, 0-length body handling is mistakable.
					sample.AppendHeader(
						"HEAD / HTTP/1.1",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					sample.AppendHeader(
						"CONNECT www.example.org:443 HTTP/1.1",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// The body length should be in range of 'tiny' length.
					// "prefetched bytes" occurs when the body is 'tiny' length for simple body.
					// In this case, whole three messages are on one memory block. 
					Debug.Assert(sample.SampleWriterPosition < ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					// ACT & ASSERT
					int counter = 0;
					int actualMessageCount = TestReadHeaderAndRedirect(sample, (request) => {
						switch (counter) {
							case 0:
								Assert.Equal("GET", request.Method);
								Assert.Equal(3, request.ContentLength);
								break;
							case 1:
								Assert.Equal("HEAD", request.Method);
								Assert.Equal(0, request.ContentLength);
								break;
							case 2:
								Assert.Equal("CONNECT", request.Method);
								Assert.Equal(0, request.ContentLength);
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
						"GET / HTTP/1.1",
						"Host: www.example.org",
						"Content-Length: 3",
						""
					);
					sample.AppendBody("ABC");

					// insert 0-length body message in the middle of sequence
					// In prefetched bytes processing, 0-length body handling is mistakable.
					sample.AppendHeader(
						"HEAD / HTTP/1.1",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					sample.AppendHeader(
						"CONNECT www.example.org:443 HTTP/1.1",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					// The body length should be in range of 'tiny' length.
					// "prefetched bytes" occurs when the body is 'tiny' length for simple body.
					// In this case, whole three messages are on one memory block. 
					Debug.Assert(sample.SampleWriterPosition < ComponentFactory.MemoryBlockCache.MemoryBlockSize);

					// ACT & ASSERT
					int counter = 0;
					int actualMessageCount = TestReadHeaderAndSkipBody(sample, (request) => {
						switch (counter) {
							case 0:
								Assert.Equal("GET", request.Method);
								Assert.Equal(3, request.ContentLength);
								break;
							case 1:
								Assert.Equal("HEAD", request.Method);
								Assert.Equal(0, request.ContentLength);
								break;
							case 2:
								Assert.Equal("CONNECT", request.Method);
								Assert.Equal(0, request.ContentLength);
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
						"GET / HTTP/1.1",
						"Host: www.example.org",
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
						"HEAD / HTTP/1.1",
						"Host: www.example.org",
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
						"CONNECT www.example.org:443 HTTP/1.1",
						"Transfer-Encoding: chunked",
						""
					);
					sample.AppendSimpleChunk(0x10);
					sample.AppendLastChunk();

					// ACT & ASSERT
					int counter = 0;
					int actualMessageCount = TestReadHeaderAndRedirect(sample, (request) => {
						switch (counter) {
							case 0:
								Assert.Equal("GET", request.Method);
								Assert.Equal(-1, request.ContentLength);
								break;
							case 1:
								Assert.Equal("HEAD", request.Method);
								Assert.Equal(-1, request.ContentLength);
								break;
							case 2:
								Assert.Equal("CONNECT", request.Method);
								Assert.Equal(-1, request.ContentLength);
								break;
						}
						++counter;
					});
					Assert.Equal(3, actualMessageCount);
					sample.AssertOutputEqualToSample();
				}
			}

			[Fact(DisplayName = "prefetched bytes: InputReconnected")]
			public void PrefetchedBytes_InputReconnect() {
				using (MessageSample sample = CreateMessageSample()) {
					// ARRANGE
					sample.AppendHeader(
						"GET / HTTP/1.1",
						"Host: www.example.org",
						"Content-Length: 3",
						""
					);
					sample.AppendBody("ABC");

					sample.AppendHeader(
						"HEAD / HTTP/1.1",
						"Host: www.example.org",
						""
					);
					sample.AppendBody(MessageSample.EmptyBody);

					long thirdMessageOffset = sample.SampleWriterPosition;
					sample.AppendHeader(
						"CONNECT www.example.org:443 HTTP/1.1",
						"Host: www.example.org",
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
						"GET / HTTP/1.1",
						"Host: www.example.org",
						"Content-Length: 3",
						"",
						"ABCCONNECT www.example.org:443 HTTP/1.1",	// Note no CRLF at the end of the body
						"Host: www.example.org",
						"",
						MessageSample.EmptyBody
					);

					// ACT & ASSERT
					int counter = 0;
					int actualMessageCount = TestReadAndWrite(sample, (request) => {
						switch (counter) {
							case 0:
								Assert.Equal("GET", request.Method);
								Assert.Equal(3, request.ContentLength);
								// move to the third message
								// It causes InputReconnected event.
								sample.ChangeSampleReaderPosition(thirdMessageOffset);
								break;
							case 1:
								// the third message instead of the second message
								Assert.Equal("CONNECT", request.Method);
								Assert.Equal(0, request.ContentLength);
								break;
						}
						++counter;
					});
					Assert.Equal(2, actualMessageCount);
					sample.AssertOutputEqualTo(expectedOutput);
				}
			}

			#endregion
		}

		public class ReadAndWrite: ReadAndWriteBasicTest<Request> {
			#region types

			private class RequestAdapter: IAdapter {
				#region properties

				public static readonly RequestAdapter Instance = new RequestAdapter();

				#endregion


				#region IAdapter

				public Request Create(IMessageIO io) {
					Request request = new Request();
					request.AttachIO(io);
					return request;
				}

				public bool Read(Request message, Request request) {
					return message.Read();
				}

				public void Write(Request message, bool suppressModification) {
					message.Write(suppressModification);
				}

				public bool ReadHeader(Request message, Request request) {
					return message.ReadHeader();
				}

				public void SkipBody(Request message) {
					message.SkipBody();
				}

				public void Redirect(Request message, bool suppressModification) {
					message.Redirect(suppressModification);
				}

				#endregion
			}

			#endregion


			#region creation

			public ReadAndWrite(): base(RequestAdapter.Instance) {
			}

			#endregion
		}

		#endregion
	}
}
