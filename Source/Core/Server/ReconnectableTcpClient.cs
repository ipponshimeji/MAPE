using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using MAPE.Utils;


namespace MAPE.Server {
    public class ReconnectableTcpClient: IDisposable {
		#region types

		private class ReconnectableStream: Stream {
			#region data

			public readonly ReconnectableTcpClient Owner;

			#endregion


			#region properties

			private Stream InnerStream {
				get {
					Stream value = this.Owner.networkStream;
					if (value == null) {
						throw new InvalidOperationException("Not connected now.");
					}

					return value;
				}
			}

			#endregion


			#region creation and disposal

			public ReconnectableStream(ReconnectableTcpClient owner) {
				// argument checks
				Debug.Assert(owner != null);

				// initialize members
				this.Owner = owner;

				return;
			}

			protected override void Dispose(bool disposing) {
				// dispose this class level
				if (disposing) {
					// dispose the inner stream
					this.Owner.DisposeStream();
				}

				// dispose the base class level
				base.Dispose(disposing);
			}

			#endregion


			#region methods

			public override bool CanRead {
				get {
					return this.InnerStream.CanRead;
				}
			}

			public override bool CanSeek {
				get {
					return this.InnerStream.CanSeek;
				}
			}

			public override bool CanWrite {
				get {
					return this.InnerStream.CanWrite;
				}
			}

			public override long Length {
				get {
					return this.InnerStream.Length;
				}
			}

			public override long Position {
				get {
					return this.InnerStream.Position;
				}
				set {
					this.InnerStream.Position = value;
				}
			}

			public override void Flush() {
				this.InnerStream.Flush();
			}

			public override int Read(byte[] buffer, int offset, int count) {
				return this.InnerStream.Read(buffer, offset, count);
			}

			public override int ReadByte() {
				return this.InnerStream.ReadByte();
			}

			public override long Seek(long offset, SeekOrigin origin) {
				return this.InnerStream.Seek(offset, origin);
			}

			public override void SetLength(long value) {
				this.InnerStream.SetLength(value);
			}

			public override void Write(byte[] buffer, int offset, int count) {
				this.InnerStream.Write(buffer, offset, count);
			}

			public override void WriteByte(byte value) {
				this.InnerStream.WriteByte(value);
			}

			#endregion
		}

		#endregion


		#region data - synchronized by locking this

		private string host = null;

		private int port = 0;

		private TcpClient tcpClient = null;

		private NetworkStream networkStream = null;

		private bool reconnectable = true;

		#endregion


		#region properties

		public string Host {
			get {
				return this.host;
			}
		}

		public int Port {
			get {
				return this.port;
			}
		}

		public string EndPoint {
			get {
				return $"{this.host}:{this.port}";
			}
		}

		public bool Reconnectable {
			get {
				return this.reconnectable;
			}
			set {
				lock (this) {
					this.reconnectable = value;
				}
			}
		}

		public Socket Client {
			get {
				return this.tcpClient?.Client;
			}
		}

		#endregion


		#region creation and disposal

		public ReconnectableTcpClient() {
		}

		public void Dispose() {
			lock (this) {
				// dispose networkStream and tcpClient
				Disconnect();
				Debug.Assert(this.networkStream == null);
				Debug.Assert(this.tcpClient == null);
				this.host = null;
			}

			return;
		}

		#endregion


		#region methods

		public void EnsureConnect(string host, int port) {
			// argument checks
			if (string.IsNullOrEmpty(host)) {
				throw new ArgumentNullException(nameof(host));
			}
			if (port < IPEndPoint.MinPort || IPEndPoint.MaxPort < port) {
				throw new ArgumentOutOfRangeException(nameof(port));
			}

			lock (this) {
				// state checks
				if (this.tcpClient != null) {
					// connecting now
					if (port == this.port && AreSameHostNames(host, this.host)) {
						// the current connection is usable
						return;
					}

					// disconnect to re-connect the connection
					DisconnectInternal();
				}
				if (this.reconnectable == false) {
					throw new InvalidOperationException("This object is not reconnectable currently.");
				}

				// keep host and port
				this.host = host;
				this.port = port;

				// connect to the end point
				ConnectInternal();
			}

			return;
		}

		public void EnsureConnect() {
			lock (this) {
				// state checks
				if (this.tcpClient != null) {
					// connecting now
					return;
				}
				if (string.IsNullOrEmpty(this.host)) {
					throw new InvalidOperationException("The host is not specified.");
				}
				if (this.reconnectable == false) {
					throw new InvalidOperationException("This object is not reconnectable currently.");
				}

				// connect to the end point
				ConnectInternal();
			}

			return;
		}

		public void Disconnect() {
			lock (this) {
				// state checks
				if (this.tcpClient == null) {
					// not connecting now
					return;
				}

				DisconnectInternal();
			}
		}

		public Stream GetStream() {
			lock (this) {
				return new ReconnectableStream(this);
			}
		}

		#endregion


		#region methods - for ReconnectableStream class only

		private void DisposeStream() {
			lock (this) {
				Util.DisposeWithoutFail(ref this.networkStream);
			}
		}

		#endregion


		#region privates

		private static bool AreSameHostNames(string name1, string name2) {
			return string.Compare(name1, name2, StringComparison.OrdinalIgnoreCase) == 0;
		}

		private void ConnectInternal() {
			// state checks
			Debug.Assert(string.IsNullOrEmpty(this.host) == false);
			Debug.Assert(IPEndPoint.MinPort <= port && port <= IPEndPoint.MaxPort);

			// connect to the end point
			TcpClient tcpClient = null;
			NetworkStream networkStream = null;
			try {
				tcpClient = new TcpClient();
				tcpClient.Connect(this.host, this.port);
				networkStream = tcpClient.GetStream();
			} catch {
				Util.DisposeWithoutFail(networkStream);
				Util.DisposeWithoutFail(tcpClient);
				throw;
			}

			// update its state
			this.tcpClient = tcpClient;
			this.networkStream = networkStream;

			return;
		}

		private void DisconnectInternal() {
			// state checks
			Debug.Assert(this.tcpClient != null);

			// dispose connection resources
			Util.DisposeWithoutFail(ref this.networkStream);
			Util.DisposeWithoutFail(ref this.tcpClient);

			return;
		}

		#endregion
	}
}
