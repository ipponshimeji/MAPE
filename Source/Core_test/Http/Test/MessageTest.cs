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
		#region utilities

		public static MessageSample CreateMessageSample(bool largeMessage = false) {
			if (largeMessage) {
				return new DiskMessageSample();
			} else {
				return new MemoryMessageSample();
			}
		}

		#endregion


		#region test bases

		public abstract class ReadAndWriteTestBase<TMessage> where TMessage : Message {
			#region types

			public interface IAdapter {
				TMessage Create(IMessageIO io);

				bool Read(TMessage message, Request request);

				void Write(TMessage message, bool suppressModification);

				bool ReadHeader(TMessage message, Request request);

				void SkipBody(TMessage message);

				void Redirect(TMessage message, bool suppressModification);
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


			#region testers

			protected int TestReadAndWrite(MessageSample sample, Action<TMessage> handler = null, Request request = null, bool suppressModification = false) {
				// argument checks
				Debug.Assert(sample != null);
				sample.CompleteArranging();
				// handler can be null
				// request can be null

				// state checks
				IAdapter adapter = this.Adapter;
				Debug.Assert(adapter != null);

				// create a Message and call Read and Write
				int messageCount = 0;
				using (TMessage message = adapter.Create(sample)) {
					Assert.Equal(MessageReadingState.None, message.ReadingState);

					// Read
					while (adapter.Read(message, request)) {
						Assert.Equal(MessageReadingState.Body, message.ReadingState);
						++messageCount;

						// handle the message
						handler?.Invoke(message);

						// Write
						adapter.Write(message, suppressModification);
					}
				}

				return messageCount;
			}

			protected int TestReadHeaderAndRedirect(MessageSample sample, Action<TMessage> handler = null, Request request = null, bool suppressModification = false) {
				// argument checks
				Debug.Assert(sample != null);
				sample.CompleteArranging();
				// handler can be null
				// request can be null

				// state checks
				IAdapter adapter = this.Adapter;
				Debug.Assert(adapter != null);

				// create a Message and call ReadHeader and Redirect
				int messageCount = 0;
				using (TMessage message = adapter.Create(sample)) {
					Assert.Equal(MessageReadingState.None, message.ReadingState);

					// ReadHeader
					while (adapter.ReadHeader(message, request)) {
						Assert.Equal(MessageReadingState.Header, message.ReadingState);
						++messageCount;

						// handle the message
						handler?.Invoke(message);

						// Redirect
						adapter.Redirect(message, suppressModification);
						Assert.Equal(MessageReadingState.BodyRedirected, message.ReadingState);
					}
				}

				return messageCount;
			}

			protected int TestReadHeaderAndSkipBody(MessageSample sample, Action<TMessage> handler = null, Request request = null) {
				// argument checks
				Debug.Assert(sample != null);
				sample.CompleteArranging();
				// handler can be null
				// request can be null

				// state checks
				IAdapter adapter = this.Adapter;
				Debug.Assert(adapter != null);

				// create a Message and call ReadHeader and SkipBody
				int messageCount = 0;
				using (TMessage message = adapter.Create(sample)) {
					Assert.Equal(MessageReadingState.None, message.ReadingState);

					// ReadHeader
					while (adapter.ReadHeader(message, request)) {
						Assert.Equal(MessageReadingState.Header, message.ReadingState);
						++messageCount;

						// handle the message
						handler?.Invoke(message);

						// SkipBody
						adapter.SkipBody(message);
						Assert.Equal(MessageReadingState.BodyRedirected, message.ReadingState);
					}
				}

				return messageCount;
			}

			#endregion


			#region tests

			[Fact(DisplayName = "Read: empty input")]
			public void Read_Empty() {
				using (Stream input = new MemoryStream()) {
					MessageIO io = new MessageIO(input, output: null);
					IAdapter adapter = this.Adapter;
					using (TMessage message = adapter.Create(io)) {
						// ACT
						Debug.Assert(input.Length == 0);
						bool actual = adapter.Read(message, null);

						// ASSERT
						Assert.Equal(false, actual);
						Assert.Equal(MessageReadingState.None, message.ReadingState);
					}
				}
			}

			[Fact(DisplayName = "ReadHeader: empty input")]
			public void ReadHeader_Empty() {
				using (Stream input = new MemoryStream()) {
					MessageIO io = new MessageIO(input, output: null);
					IAdapter adapter = this.Adapter;
					using (TMessage message = adapter.Create(io)) {
						// ACT
						Debug.Assert(input.Length == 0);
						bool actual = adapter.ReadHeader(message, null);

						// ASSERT
						Assert.Equal(false, actual);
						Assert.Equal(MessageReadingState.None, message.ReadingState);
					}
				}
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
