using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Xunit;


namespace MAPE.Testing {
	public static class TestUtil {
		#region methods

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

		#endregion
	}
}
