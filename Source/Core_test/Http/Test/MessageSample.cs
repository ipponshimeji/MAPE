using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;


namespace MAPE.Http.Test {
	public abstract class MessageSample: IDisposable, IMessageIO {
		#region types

		private class AdapterStream: Stream {
			#region data

			private readonly MessageSample owner;

			private readonly Stream innerStream;

			#endregion


			#region creation & disposal

			public AdapterStream(MessageSample owner, Stream innerStream) {
				// argument checks
				Debug.Assert(owner != null);
				Debug.Assert(innerStream != null);

				// initialize members
				this.owner = owner;
				this.innerStream = innerStream;

				return;
			}

			#endregion


			#region overrides

			public override bool CanRead {
				get {
					return this.innerStream.CanRead;
				}
			}

			public override bool CanSeek {
				get {
					return this.innerStream.CanSeek;
				}
			}

			public override bool CanTimeout {
				get {
					return this.innerStream.CanTimeout;
				}
			}

			public override bool CanWrite {
				get {
					return this.innerStream.CanWrite;
				}
			}

			public override long Length {
				get {
					return this.innerStream.Length;
				}
			}

			public override long Position {
				get {
					return this.innerStream.Position;
				}
				set {
					this.innerStream.Position = value;
				}
			}

			public override int ReadTimeout {
				get {
					return this.innerStream.ReadTimeout;
				}
				set {
					this.innerStream.ReadTimeout = value;
				}
			}

			public override int WriteTimeout {
				get {
					return this.innerStream.WriteTimeout;
				}
				set {
					this.innerStream.WriteTimeout = value;
				}
			}

			public override void Close() {
				// Do not close this.innerStream. This is just an adapter.
				base.Close();
			}

			protected override void Dispose(bool disposing) {
				// Do not dispose this.innerStream. This is just an adapter.
				base.Dispose(disposing);
			}

			public override void Flush() {
				this.innerStream.Flush();
				this.owner.OnOutputWriterFlush(this.innerStream.Position);
			}

			public override int Read(byte[] buffer, int offset, int count) {
				return this.innerStream.Read(buffer, offset, count);
			}

			public override int ReadByte() {
				return this.innerStream.ReadByte();
			}

			public override long Seek(long offset, SeekOrigin origin) {
				return this.innerStream.Seek(offset, origin);


			}

			public override void SetLength(long value) {
				this.innerStream.SetLength(value);
			}

			public override void Write(byte[] buffer, int offset, int count) {
				this.innerStream.Write(buffer, offset, count);
			}

			public override void WriteByte(byte value) {
				this.innerStream.WriteByte(value);
			}

			#endregion
		}

		#endregion


		#region constants

		public const string CRLF = "\x000D\x000A";    // CRLF

		public static readonly byte[] CRLFBytes = new byte[] { 0x0D, 0x0A };

		public static readonly string EmptyBody = string.Empty;

		#endregion


		#region data

		public static readonly Encoding MessageEncoding;

		private static Random random = new Random();


		public bool CheckChunkFlushing { get; set; } = false;

		protected MessageSampleStage Stage { get; private set; } = MessageSampleStage.Initial;

		private Stream sampleWriter = null;

		private Stream sampleReader = null;

		private Stream outputWriter = null;

		private Stream originalOutputWriter = null;

		private Stream outputReader = null;

		private EventHandler inputReconnected = null;

		private List<long> chunkEnds = null;

		private int nextChunkEndIndex = 0;

		private long sampleReadLength = 0;

		#endregion


		#region properties

		public long SampleWriterPosition {
			get {
				Stream value = this.sampleWriter;
				if (value == null) {
					throw CreateNotArrangingStageException();
				}

				return value.Position;
			}
		}

		public Stream SampleReader {
			get {
				Stream value = this.sampleReader;
				if (value == null) {
					throw CreateNotActingStageException();
				}

				return value;
			}				
		}

		public Stream OutputWriter {
			get {
				Stream value = this.outputWriter;
				if (value == null) {
					if (this.Stage != MessageSampleStage.Acting) {
						throw CreateNotActingStageException();
					} else {
						value = CreateOutputWriter();
						Debug.Assert(value.CanWrite);

						Stream originalOutputWriter;
						if (this.chunkEnds == null) {
							originalOutputWriter = value;
						} else {
							// use the adapter to detect chunk points
							originalOutputWriter = value;
							value = new AdapterStream(this, originalOutputWriter);
						}
						this.outputWriter = value;
						this.originalOutputWriter = originalOutputWriter;
					}
				}

				return value;
			}
		}

		protected Stream OutputReader {
			get {
				return this.outputReader;
			}
		}

		#endregion


		#region creation & disposal

		static MessageSample() {
			// create encoding with exception fallbacks
			// In MessageSample, text must be exactly in ASCII range.  
			MessageEncoding = Encoding.GetEncoding("us-ascii", new EncoderExceptionFallback(), new DecoderExceptionFallback());
		}

		protected MessageSample() {
		}

		public virtual void Dispose() {
			this.Stage = MessageSampleStage.Disposed;
		}

		#endregion


		#region IMessageIO

		Stream IMessageIO.Input {
			get {
				return this.SampleReader;
			}
		}

		Stream IMessageIO.Output {
			get {
				// Note that the OutputWriter is created on demand.
				return this.OutputWriter;
			}
		}

		event EventHandler IMessageIO.InputReconnected {
			add {
				inputReconnected += value;
			}
			remove {
				inputReconnected -= value;
			}
		}
		
		#endregion


		#region methods - utilities

		public static void WriteCRLFTo(Stream stream) {
			// argument checks
			if (stream == null) {
				throw new ArgumentNullException(nameof(stream));
			}

			// write CRLF to the stream
			stream.Write(CRLFBytes, 0, CRLFBytes.Length);
		}

		public static void WriteTextTo(Stream stream, string text, bool appendCRLF = false) {
			// argument checks
			if (stream == null) {
				throw new ArgumentNullException(nameof(stream));
			}
			// text can be null

			// write the text in ASCII encoding
			if (text != null) {
				byte[] bytes = MessageEncoding.GetBytes(text);
				stream.Write(bytes, 0, bytes.Length);
			}
			if (appendCRLF) {
				WriteCRLFTo(stream);
			}

			return;
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
				WriteTextTo(stream, line, true);
			}

			return;
		}

		public static void WriteRandomDataTo(long length, Stream stream1, Stream stream2 = null, bool appendCRLF = false) {
			// argument checks
			if (length < 0) {
				throw new ArgumentOutOfRangeException(nameof(length));
			}
			if (stream1 == null) {
				throw new ArgumentNullException(nameof(stream1));
			}
			// stream2 can be null

			// write random body to the streams
			byte[] buf = new byte[1024];
			while (0 < length) {
				int writeLen = checked((int)Math.Min(length, buf.Length));
				for (int i = 0; i < writeLen; ++i) {
					// give value in range of [0x30, 0x7F), that is, ['0', '~']
					// so that the data is readable when it displayed
					buf[i] = (byte)(random.Next(0x4F) + 0x30);
				}
				stream1.Write(buf, 0, writeLen);
				if (stream2 != null) {
					stream2.Write(buf, 0, writeLen);
				}

				length -= writeLen;
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

		public static void WriteRandomDataTo(long length, Stream stream, bool appendCRLF) {
			WriteRandomDataTo(length, stream, null, appendCRLF);
		}

		public static void AssertEqualContents(Stream expected, Stream actual, long length) {
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
				int readLen = checked((int)Math.Min(length, BufferSize));
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

		#endregion


		#region methods - general

		public void CompleteArranging() {
			// state checks
			switch (this.Stage) {
				case MessageSampleStage.Arranging:
					break;
				case MessageSampleStage.Acting:
					// already completed
					return;
				default:
					throw CreateNotArrangingStageException();
			}

			// complete sample writing and open sampleReader
			Stream sampleWriter = this.sampleWriter;
			this.sampleWriter = null;

			Debug.Assert(this.sampleReader == null);
			this.sampleReader = CompleteSampleWriting(sampleWriter);

			// update the state
			this.Stage = MessageSampleStage.Acting;
			this.nextChunkEndIndex = 0;

			return;
		}

		public void CompleteActing() {
			// state checks
			switch (this.Stage) {
				case MessageSampleStage.Acting:
					break;
				case MessageSampleStage.Asserting:
					// already completed
					return;
				default:
					throw CreateNotActingStageException();
			}

			// complete output writing and open outputReader
			// Note outputWriter and outputReader may be null.
			Stream originalOutputWriter = this.originalOutputWriter;
			this.originalOutputWriter = null;
			this.outputWriter = null;

			Debug.Assert(this.outputReader == null);
			this.outputReader = CompleteOutputWriting(originalOutputWriter);

			// update the state
			this.Stage = MessageSampleStage.Asserting;
			this.sampleReadLength = this.sampleReader.Position;

			return;
		}


		protected Stream EnsureArrangingStage() {
			// state checks
			Stream sampleWriter = this.sampleWriter;
			if (sampleWriter == null) {
				if (this.Stage != MessageSampleStage.Initial) {
					throw CreateNotArrangingStageException();
				} else {
					sampleWriter = CreateSampleWriter();
					this.sampleWriter = sampleWriter;
					this.Stage = MessageSampleStage.Arranging;
					this.chunkEnds = null;
				}
			}
			Debug.Assert(this.Stage == MessageSampleStage.Arranging);

			return sampleWriter;
		}

		protected Stream EnsureAssertingStage() {
			// state checks
			if (this.Stage != MessageSampleStage.Asserting) {
				CompleteActing();	// may throw an exception if in invalid stage now
			}

			return this.outputReader;
		}

		#endregion


		#region methods - arranging

		public void AppendCRLF() {
			// state checks
			Stream sampleWriter = EnsureArrangingStage();
			Debug.Assert(sampleWriter != null);

			WriteCRLFTo(sampleWriter);
		}

		public void AppendText(string text, bool appendCRLF = false) {
			// state checks
			Stream sampleWriter = EnsureArrangingStage();
			Debug.Assert(sampleWriter != null);

			WriteTextTo(sampleWriter, text, appendCRLF);
		}

		public void AppendLines(params string[] lines) {
			// state checks
			Stream sampleWriter = EnsureArrangingStage();
			Debug.Assert(sampleWriter != null);

			WriteLinesTo(sampleWriter, lines);
		}

		public void AppendRandomData(long length, Stream carbonCopy = null, bool appendCRLF = false) {
			// state checks
			Stream sampleWriter = EnsureArrangingStage();
			Debug.Assert(sampleWriter != null);

			WriteRandomDataTo(length, sampleWriter, carbonCopy, appendCRLF);
		}

		public void AppendRandomData(long length, bool appendCRLF) {
			AppendRandomData(length, null, appendCRLF);
		}


		public void AppendHeader(params string[] lines) {
			AppendLines(lines);
		}

		public void AppendBody(string text = null) {
			AppendText(text, appendCRLF: false);
		}

		public void AppendChunkSizeLine(string text) {
			// state checks
			Stream sampleWriter = EnsureArrangingStage();
			Debug.Assert(sampleWriter != null);

			// save the end of the previous chunk
			if (this.CheckChunkFlushing) {
				if (this.chunkEnds == null) {
					// the first chunk just prepares the list
					this.chunkEnds = new List<long>();
				} else {
					// save the end of the previous chunk
					this.chunkEnds.Add(sampleWriter.Position);
				}
			}

			AppendText(text, appendCRLF: true);
		}

		public void AppendRandomChunkData(long length) {
			AppendRandomData(length, appendCRLF: true);
		}

		public void AppendSimpleChunk(long length) {
			AppendChunkSizeLine($"{length.ToString("X")}");
			AppendRandomChunkData(length);
		}

		public void AppendLastChunk(params string[] trailers) {
			AppendChunkSizeLine("0");
			AppendLines(trailers);
			AppendCRLF();
		}

		#endregion


		#region methods - acting

		public void ChangeSampleReaderPosition(long position) {
			// state checks
			if (this.Stage != MessageSampleStage.Acting) {
				throw CreateNotActingStageException();
			}

			// change the position of the SampleReader
			this.SampleReader.Position = position;
			if (this.inputReconnected != null) {
				this.inputReconnected(this, EventArgs.Empty);
			}

			return;
		}

		#endregion


		#region methods - asserting

		public void AssertOutputEqualToSample() {
			// state checks
			Stream outputReader = EnsureAssertingStage();
			if (outputReader == null) {
				throw CreateNoOutputException();
			}

			Stream sampleReader = this.SampleReader;

			// compare the output with the sample
			long length = sampleReader.Length;
			Assert.Equal(length, outputReader.Length);

			sampleReader.Position = 0;
			outputReader.Position = 0;
			AssertEqualContents(sampleReader, outputReader, length);

			if (this.CheckChunkFlushing) {
				AssertChunkEnds();
			}

			return;
		}

		public void AssertOutputEqualTo(string expected) {
			// argument checks
			if (expected == null) {
				throw new ArgumentNullException(nameof(expected));
			}

			// state checks
			Stream outputReader = EnsureAssertingStage();
			if (outputReader == null) {
				throw CreateNoOutputException();
			}

			// convert output bytes to string
			string actual;
			const int bufSize = 1024;   // same to .NET Framework implementation
			outputReader.Position = 0;
			using (StreamReader reader = new StreamReader(outputReader, MessageEncoding, false, bufSize, leaveOpen: true)) {
				actual = reader.ReadToEnd();
			}

			// assert
			Assert.Equal(expected, actual);
			if (this.CheckChunkFlushing) {
				AssertChunkEnds();
			}

			return;
		}

		public void AssertAllSampleBytesRead() {
			// state checks
			EnsureAssertingStage();

			// assert whether all the sample bytes are read
			Stream sampleReader = this.SampleReader;
			Assert.Equal(sampleReader.Length, this.sampleReadLength);

			return;
		}

		public void AssertChunkEnds() {
			// state checks
			EnsureAssertingStage();
			List<long> chunkEnds = this.chunkEnds;
			if (chunkEnds == null) {
				// the body is not chunked
				return;
			}

			// assert
			int nextIndex = this.nextChunkEndIndex;
			if (nextIndex < chunkEnds.Count) {
				// there is a chunk at which the body was not flushed.
				string message = $"The chunk (index: {nextIndex}) was not flushed. Its end offset: {chunkEnds[nextIndex]}";
				throw new Xunit.Sdk.XunitException(message);
			}

			return;
		}

		#endregion


		#region overridables

		protected abstract Stream CreateSampleWriter();

		protected abstract Stream CompleteSampleWriting(Stream sampleWriter);

		protected abstract Stream CreateOutputWriter();

		protected abstract Stream CompleteOutputWriting(Stream outputWriter);

		protected virtual void OnOutputWriterFlush(long chunkEnd) {
			// check the chunk ends
			List<long> chunkEnds = this.chunkEnds;
			if (chunkEnds != null && this.nextChunkEndIndex < chunkEnds.Count) {
				if (this.chunkEnds[this.nextChunkEndIndex] == chunkEnd) {
					// matches with the recorded chunk point
					++this.nextChunkEndIndex;
				}
			}

			return;
		}

		#endregion


		#region privates

		private static InvalidOperationException CreateNotArrangingStageException() {
			return new InvalidOperationException("It is not Arranging stage now.");
		}

		private static InvalidOperationException CreateNotActingStageException() {
			return new InvalidOperationException("It is not Acting stage now.");
		}

		private static InvalidOperationException CreateNotAssertingStageException() {
			return new InvalidOperationException("It is not Asserting stage now.");
		}

		private static InvalidOperationException CreateNoOutputException() {
			return new InvalidOperationException("The output is not captured.");
		}

		#endregion
	}
}
