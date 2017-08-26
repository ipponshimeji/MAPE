using System;
using System.Diagnostics;
using System.IO;
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

		#endregion
	}
}
