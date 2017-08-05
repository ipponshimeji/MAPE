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


		#region methods - disposing

		public static void DisposeWithoutFail(IDisposable target, string errorLogTemplate = null) {
			// argument checks
			if (target == null) {
				return;
			}

			// dispose the target
			try {
				target.Dispose();
			} catch (Exception exception) {
				// log the error
				string methodName = GetCallerMethodName();
				if (errorLogTemplate == null) {
					errorLogTemplate = "Fail to dispose object: at {1}, {0}";
				}
				string errorLog = string.Format(errorLogTemplate, exception.Message, methodName);

				Logger.LogError(null, errorLog);
			}

			return;			
		}

		public static void DisposeWithoutFail<T>(ref T target, string errorLogTemplate = null) where T: class, IDisposable {
			IDisposable temp = target;
			target = null;
			DisposeWithoutFail(temp, errorLogTemplate);
		}

		public static void ClearDisposableObjectList<T>(List<T> list, string errorLogTemplate = null) where T: IDisposable {
			// argument checks
			if (list == null) {
				return;
			}

			// dispose all items in the list
			list.ForEach((item) => { DisposeWithoutFail(item, errorLogTemplate); });

			// clear the list
			list.Clear();

			return;
		}

		public static void ClearDisposableObjectListParallelly<T>(List<T> list, string errorLogTemplate = null) where T : IDisposable {
			// argument checks
			if (list == null) {
				return;
			}

			// dispose all items in the list
			Parallel.ForEach<T>(list, (item) => { DisposeWithoutFail(item, errorLogTemplate); });

			// clear the list
			list.Clear();

			return;
		}

		#endregion


		#region methods - misc

		public static string NormalizeNullToEmpty(string value) {
			return value ?? string.Empty;
		}

		public static DnsEndPoint ParseEndPoint(string s) {
			// ToDo: can simplify?
			Uri uri = new Uri($"https://{s}", UriKind.Absolute);
			if (uri.Port == 443) {
				uri = new Uri($"http://{s}", UriKind.Absolute);
				if (uri.Port == 80) {
					throw new FormatException("The port number is indispensable.");
				}
			}

			if (string.CompareOrdinal(uri.PathAndQuery, "/") != 0 || string.IsNullOrEmpty(uri.Fragment) == false) {
				throw new FormatException("Other part than host or port is specified.");
			}

			return new DnsEndPoint(uri.Host, uri.Port);
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
