using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using MAPE.Testing;


namespace MAPE.Http.Test {
	public class MessageTest {
		#region data

		public const string CRLF = "\x000D\x000A";    // CRLF

		public static readonly byte[] CRLFBytes = new byte[] { 0x0D, 0x0A };

		public static readonly Encoding MessageEncoding = new ASCIIEncoding();

		public static readonly string EmptyBody = string.Empty;

		#endregion


		#region utilities

		private static void WriteCRLFTo(Stream stream) {
			// argument checks
			if (stream == null) {
				throw new ArgumentNullException(nameof(stream));
			}

			// write CRLF to the stream
			stream.Write(CRLFBytes, 0, CRLFBytes.Length);
		}

		#endregion


		#region test bases

		public abstract class ReadAndWriteTestBase<TMessage> where TMessage : Message {
			#region types

			public interface IAdapter {
				TMessage Create();

				bool Read(TMessage message, Stream input, Request request);

				void Write(TMessage message, Stream output, bool suppressModification);

				bool ReadHeader(TMessage message, Stream input, Request request);

				void Redirect(TMessage message, Stream output, Stream input, bool suppressModification);
			}

			#endregion


			#region data

			protected readonly IAdapter Adapter;

			#endregion


			#region creation

			protected ReadAndWriteTestBase(IAdapter adapter) {
				// argument checks
				if (adapter == null) {
					throw new ArgumentNullException(nameof(adapter));
				}

				// initialize members
				this.Adapter = adapter;

				return;
			}

			#endregion


			#region utilities

			public static string CreateMessageString(params string[] lines) {
				// argument checks
				if (lines == null) {
					throw new ArgumentNullException(nameof(lines));
				}

				// Note that CRLF is not appended after the last line.
				return string.Join(CRLF, lines);
			}

			public static void WriteLinesTo(Stream stream, params string[] lines) {
				// argument checks
				if (stream == null) {
					throw new ArgumentNullException(nameof(stream));
				}
				if (lines == null) {
					throw new ArgumentNullException(nameof(lines));
				}

				// write each line in ASCII encoding
				// Note that CRLF is appended at the end of each line.
				foreach (string line in lines) {
					byte[] bytes = MessageEncoding.GetBytes(line);
					stream.Write(bytes, 0, bytes.Length);
					WriteCRLFTo(stream);
				}

				return;
			}

			public static void WriteRandomBody(long bodyLength, Stream stream1, Stream stream2 = null, bool appendCRLF = false) {
				// argument checks
				if (bodyLength < 0) {
					throw new ArgumentOutOfRangeException(nameof(bodyLength));
				}
				if (stream1 == null) {
					throw new ArgumentNullException(nameof(stream1));
				}
				// stream2 can be null

				// write random body to the streams
				Random random = new Random();
				byte[] buf = new byte[1024];
				while (0 < bodyLength) {
					int writeLen = checked((int)Math.Min(bodyLength, buf.Length));
					for (int i = 0; i < writeLen; ++i) {
						// give value in range of [0x30, 0x7F), that is, ['0', '~']
						// so that the data is readable when it displayed
						buf[i] = (byte)(random.Next(0x4F) + 0x30);
					}
					stream1.Write(buf, 0, writeLen);
					if (stream2 != null) {
						stream2.Write(buf, 0, writeLen);
					}

					bodyLength -= writeLen;
				}

				// append CRLF if necessary
				if (appendCRLF) {
					WriteCRLFTo(stream1);
					if (stream2 != null) {
						WriteCRLFTo(stream2);
					}
				}

				return;
			}

			public static void WriteRandomBody(long bodyLength, Stream stream1, bool appendCRLF) {
				WriteRandomBody(bodyLength, stream1, null, appendCRLF);
			}

			protected static void AssertEqualContents(Stream expected, Stream actual, int length) {
				// argument checks
				if (expected == null || actual == null) {
					// passes only both expected and actual are null
					Assert.Equal(expected, actual);
					return;
				}
				if (length == 0) {
					return;     // no content to be checked
				} else if (length < 0) {
					throw new ArgumentOutOfRangeException(nameof(length));
				}
				if (expected.Length - expected.Position < length) {
					throw new ArgumentException("Its contents are smaller than the length to be checked.", nameof(expected));
				}
				if (actual.Length - actual.Position < length) {
					throw new ArgumentException("Its contents are smaller than the length to be checked.", nameof(actual));
				}

				// preparations
				const int BufferSize = 256;
				byte[] expectedBuffer = new byte[BufferSize];
				byte[] actualBuffer = new byte[BufferSize];
				Action<Stream, int, byte[]> fillBuffer = (stream, count, buf) => {
					// argument checks
					Debug.Assert(stream != null);
					Debug.Assert(0 < count);
					Debug.Assert(buf != null && count <= buf.Length);

					// fill the buffer
					int offset = 0;
					while (0 < count) {
						int readCount = stream.Read(buf, offset, count);
						if (readCount <= 0) {
							throw new Exception("Unexpected end of stream.");
						}
						offset += readCount;
						count -= readCount;
					}
				};

				// compare their contents
				int baseOffset = 0;
				while (0 < length) {
					// assert a block
					int readLen = Math.Min(length, BufferSize);
					fillBuffer(expected, readLen, expectedBuffer);
					fillBuffer(actual, readLen, actualBuffer);
					for (int i = 0; i < readLen; ++i) {
						if (expectedBuffer[i] != actualBuffer[i]) {
							string expectedText = MessageEncoding.GetString(expectedBuffer, 0, readLen);
							string actualText = MessageEncoding.GetString(actualBuffer, 0, readLen);
							int startOffset = baseOffset;
							int endOffset = baseOffset + readLen;
							string message = $"AssertEqualContents() Failure at offset {startOffset}.{Environment.NewLine}The contents in offset [{startOffset}, {endOffset}) are as follows:";
							throw new Xunit.Sdk.AssertActualExpectedException(expectedText, actualText, message);
						}
					}

					// prepare the next block
					baseOffset += readLen;
					length -= readLen;
				}

				return;
			}


			// for small message
			protected void Test(Action<Stream, Stream, Action<TMessage>, Request, bool> action, string input, string expectedOutput, Action<TMessage> handler, Request request, bool suppressModification) {
				// argument checks
				if (action == null) {
					throw new ArgumentNullException(nameof(action));
				}
				if (input == null) {
					throw new ArgumentNullException(nameof(input));
				}
				// expectedOutput can be null
				// handler can be null
				// request can be null

				// test
				string actualOutput;
				using (MemoryStream outputStream = new MemoryStream()) {
					using (Stream inputStream = new MemoryStream(MessageEncoding.GetBytes(input))) {
						// call action
						action(inputStream, outputStream, handler, request, suppressModification);
					}

					// get actual output
					int length = checked((int)outputStream.Length);
					actualOutput = MessageEncoding.GetString(outputStream.GetBuffer(), 0, length);
				}

				// assert the output as string
				Assert.Equal(expectedOutput, actualOutput);
			}

			// for large message.
			protected void Test(Action<Stream, Stream, Action<TMessage>, Request, bool> action, Stream input, Stream expectedOutput, Action<TMessage> handler, Request request, bool suppressModification) {
				// argument checks
				if (action == null) {
					throw new ArgumentNullException(nameof(action));
				}
				if (input == null) {
					throw new ArgumentNullException(nameof(input));
				}
				// expectedOutput can be null
				// handler can be null
				// request can be null

				// read and write a message
				long startPosition = input.Position;
				IAdapter adapter = this.Adapter;
				using (Stream actualOutput = TestUtil.CreateTempFileStream()) {
					action(input, actualOutput, handler, request, suppressModification);

					// assert the output
					actualOutput.Position = 0;
					if (input == expectedOutput) {
						// input is the expected output
						expectedOutput.Position = startPosition;
					}
					int length = checked((int)(expectedOutput.Length - expectedOutput.Position));
					AssertEqualContents(expectedOutput, actualOutput, length);
				}

				return;
			}

			protected void TestSimpleBody(Action<Stream, Stream, Action<TMessage>, Request, bool> action, string header, long bodyLength, Action<TMessage> handler = null, Request request = null, bool suppressModification = false) {
				// argument checks
				if (action == null) {
					throw new ArgumentNullException(nameof(action));
				}
				if (header == null) {
					throw new ArgumentNullException(nameof(header));
				}
				if (bodyLength < 0) {
					throw new ArgumentOutOfRangeException(nameof(bodyLength));
				}

				// create input with random body and test with it
				using (Stream input = TestUtil.CreateTempFileStream()) {
					// ARRANGE
					WriteLinesTo(input, header);
					WriteRandomBody(bodyLength, input);
					input.Position = 0;
					Stream expectedOutput = input;  // same to the input

					// Test
					Test(action, input, expectedOutput, handler, request, suppressModification);
				}
			}

			#endregion


			#region tester for Read and Write methods

			protected void ReadWriteAction(Stream input, Stream output, Action<TMessage> handler, Request request, bool suppressModification) {
				// argument checks
				Debug.Assert(input != null);
				Debug.Assert(output != null);
				// handler can be null
				// request can be null

				// state checks
				IAdapter adapter = this.Adapter;
				Debug.Assert(adapter != null);

				// create a Message and call Read and Write
				using (TMessage message = adapter.Create()) {
					message.AttachStreams(input, output);
					try {
						// Read
						Assert.Equal(MessageReadingState.None, message.ReadingState);
						bool actualRead = adapter.Read(message, input, request);
						Assert.Equal(true, actualRead);
						Assert.Equal(MessageReadingState.Body, message.ReadingState);

						// handle the message
						handler?.Invoke(message);

						// Write
						adapter.Write(message, output, suppressModification);
					} finally {
						message.DetachStreams();
					}
				}

				return;
			}

			// for small message
			protected void TestReadWrite(string input, string expectedOutput, Action<TMessage> handler = null, Request request = null, bool suppressModification = false) {
				Test(ReadWriteAction, input, expectedOutput, handler, request, suppressModification);
			}

			// for large message.
			protected void TestReadWrite(Stream input, Stream expectedOutput, Action<TMessage> handler = null, Request request = null, bool suppressModification = false) {
				Test(ReadWriteAction, input, expectedOutput, handler, request, suppressModification);
			}

			protected void TestReadWriteSimpleBody(string header, long bodyLength, Action<TMessage> handler = null, Request request = null, bool suppressModification = false) {
				TestSimpleBody(ReadWriteAction, header, bodyLength, handler, request, suppressModification);
			}

			#endregion


			#region tester for ReadHeader and Redirect methods

			protected void ReadHeaderRedirectTester(Stream input, Stream output, Action<TMessage> handler, Request request, bool suppressModification) {
				// argument checks
				Debug.Assert(input != null);
				Debug.Assert(output != null);
				// handler can be null
				// request can be null

				// state checks
				IAdapter adapter = this.Adapter;
				Debug.Assert(adapter != null);

				// create a Message and call ReadHeader and Redirect
				using (TMessage message = adapter.Create()) {
					message.AttachStreams(input, output);
					try {
						// ReadHeader
						Assert.Equal(MessageReadingState.None, message.ReadingState);
						bool actualRead = adapter.ReadHeader(message, input, request);
						Assert.Equal(true, actualRead);
						Assert.Equal(MessageReadingState.Header, message.ReadingState);

						// handle the message
						handler?.Invoke(message);

						// Redirect
						adapter.Redirect(message, output, input, suppressModification);
						Assert.Equal(MessageReadingState.BodyRedirected, message.ReadingState);
					} finally {
						message.DetachStreams();
					}
				}

				return;
			}

			// for small message
			protected void TestReadHeaderRedirect(string input, string expectedOutput, Action<TMessage> handler = null, Request request = null, bool suppressModification = false) {
				Test(ReadHeaderRedirectTester, input, expectedOutput, handler, request, suppressModification);
			}

			// for large message.
			protected void TestReadHeaderRedirect(Stream input, Stream expectedOutput, Action<TMessage> handler = null, Request request = null, bool suppressModification = false) {
				Test(ReadHeaderRedirectTester, input, expectedOutput, handler, request, suppressModification);
			}

			protected void TestReadHeaderRedirectSimpleBody(string header, long bodyLength, Action<TMessage> handler = null, Request request = null, bool suppressModification = false) {
				TestSimpleBody(ReadHeaderRedirectTester, header, bodyLength, handler, request, suppressModification);
			}

			#endregion


			#region tests

			[Fact(DisplayName = "empty input")]
			public void Empty() {
				// ARRANGE & ACT
				bool actual;
				using (Stream input = new MemoryStream()) {
					IAdapter adapter = this.Adapter;
					using (TMessage message = adapter.Create()) {
						message.AttachStreams(input, input);
						try {
							// act
							Debug.Assert(input.Length == 0);
							actual = adapter.Read(message, input, null);
						} finally {
							message.DetachStreams();
						}
					}
				}

				// ASSERT
				Assert.Equal(false, actual);
			}

			#endregion
		}

		#endregion


		#region AddModification

		public class AddModification {
			#region types

			public class Sample: Message {
				#region properties

				public new IReadOnlyList<MessageBuffer.Modification> Modifications {
					get {
						return base.Modifications;
					}
				}

				#endregion


				#region overridables

				protected override void ScanStartLine(HeaderBuffer headerBuffer) {
					throw new NotImplementedException();
				}

				protected override void WriteHeader(Stream output, HeaderBuffer headerBuffer, IEnumerable<MessageBuffer.Modification> modifications) {
					// do nothing
				}

				protected override void WriteBody(Stream output, BodyBuffer bodyBuffer) {
					// do nothing
				}

				#endregion
			}

			#endregion


			#region utilities

			public static Span[] ExtractSpans(IEnumerable<MessageBuffer.Modification> modifications) {
				// argument checks
				if (modifications == null) {
					throw new ArgumentNullException(nameof(modifications));
				}

				return (
					from modification in modifications
					select modification.Span
				).ToArray();
			}

			#endregion


			#region tests

			[Fact(DisplayName = "empty")]
			public void Empty() {
				// ARRANGE
				Sample sample = new Sample();

				// ACT
				// do nothing

				// ASSERT
				Assert.Equal(0, sample.Modifications.Count);
			}

			[Fact(DisplayName = "insert: first")]
			public void Insert_First() {
				// ARRANGE
				Sample sample = new Sample();
				sample.AddModification(new Span(10, 20), null);
				sample.AddModification(new Span(30, 40), null);

				// ACT
				sample.AddModification(new Span(0, 10), null);

				// ASSERT
				IReadOnlyList<MessageBuffer.Modification> actual = sample.Modifications;

				Assert.Equal(3, actual.Count);
				Assert.Equal(new Span(0, 10), actual[0].Span);
				Assert.Equal(new Span(10, 20), actual[1].Span);
				Assert.Equal(new Span(30, 40), actual[2].Span);
			}

			[Fact(DisplayName = "insert: last")]
			public void Insert_Last() {
				// ARRANGE
				Sample sample = new Sample();
				sample.AddModification(new Span(10, 20), null);
				sample.AddModification(new Span(30, 40), null);

				// ACT
				sample.AddModification(new Span(40, 50), null);

				// ASSERT
				IReadOnlyList<MessageBuffer.Modification> actual = sample.Modifications;

				Assert.Equal(3, actual.Count);
				Assert.Equal(new Span(10, 20), actual[0].Span);
				Assert.Equal(new Span(30, 40), actual[1].Span);
				Assert.Equal(new Span(40, 50), actual[2].Span);
			}

			[Fact(DisplayName = "insert: middle")]
			public void Insert_Middle() {
				// ARRANGE
				Sample sample = new Sample();
				sample.AddModification(new Span(10, 20), null);
				sample.AddModification(new Span(30, 40), null);

				// ACT
				sample.AddModification(new Span(20, 30), null);

				// ASSERT
				IReadOnlyList<MessageBuffer.Modification> actual = sample.Modifications;

				Assert.Equal(3, actual.Count);
				Assert.Equal(new Span(10, 20), actual[0].Span);
				Assert.Equal(new Span(20, 30), actual[1].Span);
				Assert.Equal(new Span(30, 40), actual[2].Span);
			}

			[Fact(DisplayName = "insert: middle, discrete")]
			public void Insert_Middle_Discrete() {
				// ARRANGE
				Sample sample = new Sample();
				sample.AddModification(new Span(10, 20), null);
				sample.AddModification(new Span(30, 40), null);

				// ACT
				sample.AddModification(new Span(24, 26), null);

				// ASSERT
				IReadOnlyList<MessageBuffer.Modification> actual = sample.Modifications;

				Assert.Equal(3, actual.Count);
				Assert.Equal(new Span(10, 20), actual[0].Span);
				Assert.Equal(new Span(24, 26), actual[1].Span);
				Assert.Equal(new Span(30, 40), actual[2].Span);
			}

			[Fact(DisplayName = "overlapped")]
			public void Overlapped() {
				// ARRANGE
				Sample sample = new Sample();
				sample.AddModification(new Span(10, 20), null);
				sample.AddModification(new Span(30, 40), null);

				// ACT & ASSERT
				Assert.Throws<ArgumentException>(() => {
					sample.AddModification(new Span(38, 45), null);
				});
			}

			[Fact(DisplayName = "contained")]
			public void Contained() {
				// ARRANGE
				Sample sample = new Sample();
				sample.AddModification(new Span(10, 20), null);
				sample.AddModification(new Span(30, 40), null);

				// ACT & ASSERT
				Assert.Throws<ArgumentException>(() => {
					sample.AddModification(new Span(15, 18), null);
				});
			}

			[Fact(DisplayName = "insert at the sampe point")]
			public void InsertAtSamePoint() {
				// ARRANGE
				Sample sample = new Sample();
				sample.AddModification(new Span(10, 10), null);
				sample.AddModification(new Span(10, 10), null);
				Func<Modifier, bool> handler = (modifier) => false;

				// ACT
				sample.AddModification(new Span(10, 10), handler);

				// ASSERT
				IReadOnlyList<MessageBuffer.Modification> actual = sample.Modifications;

				// Note that the added modification is appended at the last
				Assert.Equal(3, actual.Count);
				Assert.Equal(new Span(10, 10), actual[0].Span);
				Assert.Equal(null, actual[0].Handler);
				Assert.Equal(new Span(10, 10), actual[1].Span);
				Assert.Equal(null, actual[1].Handler);
				Assert.Equal(new Span(10, 10), actual[2].Span);
				Assert.Equal(handler, actual[2].Handler);
			}

			#endregion
		}

		#endregion
	}
}
