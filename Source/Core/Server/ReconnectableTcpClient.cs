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
					return this.Owner.networkStream;
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
					lock (this.Owner) {
						return this.InnerStream.CanRead;
					}
				}
			}

			public override bool CanSeek {
				get {
					lock (this.Owner) {
						return this.InnerStream.CanSeek;
					}
				}
			}

			public override bool CanWrite {
				get {
					lock (this.Owner) {
						return this.InnerStream.CanWrite;
					}
				}
			}

			public override long Length {
				get {
					lock (this.Owner) {
						return this.InnerStream.Length;
					}
				}
			}

			public override long Position {
				get {
					lock (this.Owner) {
						return this.InnerStream.Position;
					}
				}
				set {
					lock (this.Owner) {
						this.InnerStream.Position = value;
					}
				}
			}

			public override void Flush() {
				lock (this.Owner) {
					this.InnerStream.Flush();
				}
			}

			public override int Read(byte[] buffer, int offset, int count) {
				lock (this.Owner) {
					return this.InnerStream.Read(buffer, offset, count);
				}
			}

			public override int ReadByte() {
				lock (this.Owner) {
					return this.InnerStream.ReadByte();
				}
			}

			public override long Seek(long offset, SeekOrigin origin) {
				lock (this.Owner) {
					return this.InnerStream.Seek(offset, origin);
				}
			}

			public override void SetLength(long value) {
				lock (this.Owner) {
					this.InnerStream.SetLength(value);
				}
			}

			public override void Write(byte[] buffer, int offset, int count) {
				lock (this.Owner) {
					this.InnerStream.Write(buffer, offset, count);
				}
			}

			public override void WriteByte(byte value) {
				lock (this.Owner) {
					this.InnerStream.WriteByte(value);
				}
			}

			#endregion
		}

		#endregion


		#region data - synchronized by locking this

		private DnsEndPoint remoteEndPoint;

		private TcpClient tcpClient;

		private NetworkStream networkStream;

		private bool reconnectable;

		#endregion


		#region properties

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

		public ReconnectableTcpClient(DnsEndPoint endPoint) {
			// argument checks
			if (endPoint == null) {
				throw new ArgumentNullException(nameof(endPoint));
			}

			// initialize members
			this.remoteEndPoint = endPoint;
			this.tcpClient = null;
			this.networkStream = null;
			this.reconnectable = true;

			return;
		}

		public void Dispose() {
			lock (this) {
				// dispose networkStream and tcpClient
				DisconnectTcpClient();
				Debug.Assert(this.networkStream == null);
				Debug.Assert(this.tcpClient == null);

				// dispose endPoint
				this.remoteEndPoint = null;
			}

			return;
		}

		#endregion


		#region methods

		public void Connect() {
			lock (this) {
				// state checks
				if (this.remoteEndPoint == null) {
					throw new ObjectDisposedException(null);
				}
				if (this.tcpClient != null) {
					throw new InvalidOperationException("This object was already connected.");
				}

				// connect to the end point
				ConnectTcpClient(getNetworkStream: false);
			}

			return;
		}

		public Stream GetStream() {
			lock (this) {
				// state checks
				if (this.tcpClient == null) {
					throw new InvalidOperationException("This object is not connected.");
				}
				if (this.networkStream != null) {
					// this object can provide only one stream
					throw new InvalidOperationException("This object is already providing its stream.");
				}

				// capsule the network stream in a ReconnectableStream
				this.networkStream = this.tcpClient.GetStream();
				return new ReconnectableStream(this);
			}
		}

		public void Reconnect() {
			lock (this) {
				// state checks
				if (this.remoteEndPoint == null) {
					throw new ObjectDisposedException(null);
				}
				if (this.reconnectable == false) {
					throw new InvalidOperationException("This object is not reconnectable now.");
				}

				// reconnect the tcp client
				bool providingStream = this.networkStream != null;
				DisconnectTcpClient();
				ConnectTcpClient(getNetworkStream: providingStream);
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

		private void ConnectTcpClient(bool getNetworkStream) {
			// state checks
			DnsEndPoint remoteEndPoint = this.remoteEndPoint;
			Debug.Assert(remoteEndPoint != null);
			Debug.Assert(this.tcpClient == null);
			Debug.Assert(this.networkStream == null);

			// create and connect a tcp client
			TcpClient tcpClient = null;
			NetworkStream networkStream = null;
			try {
				tcpClient = new TcpClient();
				tcpClient.Connect(remoteEndPoint.Host, remoteEndPoint.Port);
				if (getNetworkStream) {
					networkStream = tcpClient.GetStream();
				}
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

		private void DisconnectTcpClient() {
			Util.DisposeWithoutFail(ref this.networkStream);
			Util.DisposeWithoutFail(ref this.tcpClient);

			return;
		}

		#endregion
	}
}
