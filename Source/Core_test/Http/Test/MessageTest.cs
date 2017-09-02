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

		public static readonly Encoding MessageEncoding = new ASCIIEncoding();

		public static readonly string EmptyBody = string.Empty;

		#endregion


		#region test bases

		public abstract class ReadAndWriteTestBase<TMessage> where TMessage : Message {
			#region types

			public interface IAdapter {
				TMessage Create();

				bool Read(TMessage message, Request request);

				void Write(TMessage message);
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
				// Note that CRLF is not appended after the last line.
				return string.Join(CRLF, lines);
			}

			public static void WriteLinesTo(Stream stream, params string[] lines) {
				// argument checks
				if (stream == null) {
					throw new ArgumentNullException(nameof(stream));
				}

				// write each line in ASCII encoding
				// Note that CRLF is appended at the end of each line.
				foreach (string line in lines) {
					byte[] bytes = MessageEncoding.GetBytes(line + CRLF);
					stream.Write(bytes, 0, bytes.Length);
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
						// so that readable when displayed
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
					buf[0] = MessageBuffer.CR;
					buf[1] = MessageBuffer.LF;
					stream1.Write(buf, 0, 2);
					if (stream2 != null) {
						stream2.Write(buf, 0, 2);
					}
				}

				return;
			}

			public static void WriteRandomBody(long bodyLength, Stream stream1, bool appendCRLF) {
				WriteRandomBody(bodyLength, stream1, null, appendCRLF);
			}

			protected void TestReadWrite(string input, string expectedOutput, Action<TMessage> action = null, Request request = null) {
				// argument checks
				if (input == null) {
					throw new ArgumentNullException(nameof(input));
				}
				// expectedOutput can be null
				// action can be null
				// request can be null

				// read and write a message
				IAdapter adapter = this.Adapter;
				string actualOutput;
				using (MemoryStream outputStream = new MemoryStream()) {
					using (Stream inputStream = new MemoryStream(MessageEncoding.GetBytes(input))) {
						using (TMessage message = adapter.Create()) {
							message.AttachStreams(inputStream, outputStream);
							try {
								bool actualRead = adapter.Read(message, request);
								Assert.Equal(true, actualRead);
								action?.Invoke(message);
								adapter.Write(message);
							} finally {
								message.DetachStreams();
							}
						}
					}

					// get actual output
					int length = checked((int)outputStream.Length);
					actualOutput = MessageEncoding.GetString(outputStream.GetBuffer(), 0, length);
				}

				// assert the output
				Assert.Equal(expectedOutput, actualOutput);
			}

			// for large message.
			protected void TestReadWrite(Stream input, Stream expectedOutput, Action<TMessage> action = null, Request request = null) {
				// argument checks
				if (input == null) {
					throw new ArgumentNullException(nameof(input));
				}
				// expectedOutput can be null
				// action can be null
				// request can be null

				// read and write a message
				long startPosition = input.Position;
				IAdapter adapter = this.Adapter;
				using (Stream actualOutput = TestUtil.CreateTempFileStream()) {
					using (TMessage message = adapter.Create()) {
						message.AttachStreams(input, actualOutput);
						try {
							adapter.Read(message, request);
							action?.Invoke(message);
							adapter.Write(message);
						} finally {
							message.DetachStreams();
						}
					}

					// assert the output
					actualOutput.Position = 0;
					if (input == expectedOutput) {
						expectedOutput.Position = startPosition;
					}
					int length = checked((int)(expectedOutput.Length - expectedOutput.Position));
					AssertEqualContents(expectedOutput, actualOutput, length);
				}

				return;
			}

			protected static void AssertEqualContents(Stream expected, Stream actual, int length) {
				// argument checks
				if (expected == null) {
					throw new ArgumentNullException(nameof(expected));
				}
				if (actual == null) {
					throw new ArgumentNullException(nameof(actual));
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

			protected void TestReadWriteSimpleBody(string header, long bodyLength, Action<TMessage> action = null, Request request = null) {
				// argument checks
				if (header == null) {
					throw new ArgumentNullException(nameof(header));
				}
				if (bodyLength < 0) {
					throw new ArgumentOutOfRangeException(nameof(bodyLength));
				}

				using (Stream input = TestUtil.CreateTempFileStream()) {
					// ARRANGE
					WriteLinesTo(input, header);
					WriteRandomBody(bodyLength, input);
					input.Position = 0;
					Stream expectedOutput = input;  // same to the input

					// Test
					TestReadWrite(input, expectedOutput, action, request);
				}
			}

			#endregion


			#region tests

			[Fact(DisplayName = "empty input")]
			public void Empty() {
				// ARRANGE & ACT
				bool actual;
				using (Stream stream = new MemoryStream()) {
					IAdapter adapter = this.Adapter;
					using (TMessage message = adapter.Create()) {
						message.AttachStreams(stream, stream);
						try {
							// act
							actual = adapter.Read(message, null);
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


		#region AppendModification

		public class AppendModification {
			#region types

			public class Sample: Message {
				#region data

				private Action<IEnumerable<MessageBuffer.Modification>> action = null;

				#endregion


				#region methods

				public void Test(Action<IEnumerable<MessageBuffer.Modification>> action) {
					this.action = action;
					try {
						Write(Stream.Null);
					} finally {
						this.action = null;
					}

					return;
				}

				#endregion


				#region overridables

				protected override void ScanStartLine(HeaderBuffer headerBuffer) {
					throw new NotImplementedException();
				}

				protected override void WriteHeader(Stream output, HeaderBuffer headerBuffer, IEnumerable<MessageBuffer.Modification> modifications) {
					this.action?.Invoke(modifications);
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

				// ASSERT
				sample.Test((modifications) => {
					Assert.Equal(null, modifications);
				});
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
				sample.Test((modifications) => {
					Span[] spans = ExtractSpans(modifications);

					Assert.Equal(3, spans.Length);
					Assert.Equal(new Span(0, 10), spans[0]);
					Assert.Equal(new Span(10, 20), spans[1]);
					Assert.Equal(new Span(30, 40), spans[2]);
				});
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
				sample.Test((modifications) => {
					Span[] spans = ExtractSpans(modifications);

					Assert.Equal(3, spans.Length);
					Assert.Equal(new Span(10, 20), spans[0]);
					Assert.Equal(new Span(30, 40), spans[1]);
					Assert.Equal(new Span(40, 50), spans[2]);
				});
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
				sample.Test((modifications) => {
					Span[] spans = ExtractSpans(modifications);

					Assert.Equal(3, spans.Length);
					Assert.Equal(new Span(10, 20), spans[0]);
					Assert.Equal(new Span(20, 30), spans[1]);
					Assert.Equal(new Span(30, 40), spans[2]);
				});
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

			#endregion
		}

		#endregion
	}
}
