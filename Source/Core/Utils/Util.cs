using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;


namespace MAPE.Utils {
	public static class Util {
		#region constants

		public const int MaxBackupHistory = 99;

		#endregion


		#region methods - misc

		public static string NormalizeNullToEmpty(string value) {
			return value ?? string.Empty;
		}

		public static string Trim(string value) {
			return (value == null) ? null : value.Trim();
		}

		public static FileStream CreateTempFileStream() {
			string tempFilePath = Path.GetTempFileName();
			try {
				int bufferSize = 4096;	// same to the default value of .NET Framework implementation
				return new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize, FileOptions.DeleteOnClose);
			} catch {
				try {
					File.Delete(tempFilePath);
				} catch {
					// continue
				}
				throw;
			}
		}

		public static DnsEndPoint ParseEndPoint(string s, bool canOmitPort = false) {
			// argument checks
//			if (string.IsNullOrEmpty(s)) {
//				throw new ArgumentNullException(nameof(s));
//			}

			// ToDo: can simplify?
			Uri uri;
			try {
				if (canOmitPort) {
					// give 80 for port if it is omitted
					uri = new Uri($"http://{s}", UriKind.Absolute);
				} else {
					uri = new Uri($"https://{s}", UriKind.Absolute);
					if (uri.Port == 443) {
						uri = new Uri($"http://{s}", UriKind.Absolute);
						if (uri.Port == 80) {
							throw new FormatException("The port number is indispensable.");
						}
					}
				}
			} catch (UriFormatException exception) {
				throw new FormatException(exception.Message);
			}
			if (string.CompareOrdinal(uri.PathAndQuery, "/") != 0 || string.IsNullOrEmpty(uri.Fragment) == false) {
				throw new FormatException("Other part than host or port is specified.");
			}

			return new DnsEndPoint(uri.Host, uri.Port);
		}

		public static bool AreSameHostNames(string name1, string name2) {
			return string.Compare(name1, name2, StringComparison.OrdinalIgnoreCase) == 0;
		}

		public static void BackupAndSave(string filePath, Action<string> saveTo, int backupHistory) {
			// argument checks
			if (filePath == null) {
				throw new ArgumentNullException(nameof(filePath));
			}
			if (saveTo == null) {
				throw new ArgumentNullException(nameof(saveTo));
			}
			if (backupHistory < 0 || MaxBackupHistory < backupHistory) {
				throw new ArgumentOutOfRangeException(nameof(backupHistory));
			}

			// create folder if it does not exist
			string folderPath = Path.GetDirectoryName(filePath);
			if (Directory.Exists(folderPath) == false) {
				Directory.CreateDirectory(folderPath);
			}

			// save to the temp file
			string tempFilePath = string.Concat(filePath, ".tmp");
			File.Delete(tempFilePath);
			saveTo(tempFilePath);

			// rotate backup files
			Func<int, string> getBackupFilePath = (history) => {
				return $"{filePath}.{history.ToString("D2")}.bak";
			};

			string oldestBackupToFilePath = getBackupFilePath(backupHistory + 1);
			string backupToFilePath = oldestBackupToFilePath;
			File.Delete(backupToFilePath);
			for (int i = backupHistory; 0 <= i; --i) {
				string backupFromFilePath = (i == 0)? filePath: getBackupFilePath(i);
				if (File.Exists(backupFromFilePath)) {
					File.Move(backupFromFilePath, backupToFilePath);
				}
				backupToFilePath = backupFromFilePath;
			}
			Debug.Assert(backupToFilePath == filePath);
			File.Move(tempFilePath, backupToFilePath);
			File.Delete(oldestBackupToFilePath);

			return;
		}

		#endregion


		#region privates

		private static string GetCallerMethodName() {
			string methodName = "(unknown method)";
			try {
				// trace stack and find the caller outside this class
				Type thisClass = typeof(Util);
				StackTrace stackTrace = new StackTrace(2);  // skip this method and direct caller
				for (int i = 0; i < stackTrace.FrameCount; ++i) {
					StackFrame frame = stackTrace.GetFrame(i);
					MethodBase method = frame.GetMethod();
					Type type = method.DeclaringType;
					if (type != thisClass) {
						// the caller who call the method of this class
						methodName = $"{type.Name}.{method.Name}()";
					}
				}
			} catch {
				// continue
			}

			return methodName;
		}

		#endregion
	}
}
