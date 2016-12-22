using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using MAPE.Utils;


namespace MAPE.Configuration {
	public class ListenerConfiguration {
		#region types

		public class ScanningAdapter: CharScanningAdapter {
			#region creation and disposal

			public ScanningAdapter(IEnumerator<char> enumerator): base(enumerator) {
			}

			#endregion


			#region methods

			public static bool IsWhitespace(char c) {
				return c == ' ' || c == '\t';
			}

			public static bool IsNotWhitespace(char c) {
				return !IsWhitespace(c);
			}

			public static bool IsGeneralSeparator(char c) {
				return IsWhitespace(c) || c == ',' || c == ';';
			}


			public bool SkipWhitespaces(bool shouldNotEnd = false) {
				return Skip(IsNotWhitespace, shouldNotEnd);
			}

			#endregion
		}

		public static class ParameterNames {
			#region constants

			public const string Backlog = "backlog";

			#endregion


			#region methods

			public static bool AreEqual(string name1, string name2) {
				return string.Compare(name1, name2, StringComparison.InvariantCultureIgnoreCase) == 0;
			}

			#endregion
		}

		#endregion


		#region constants

		public const int DefaultPort = 8888;

		public const int DefaultBacklog = 8;

		#endregion


		#region data

		private IPEndPoint endPoint;

		private int backlog;

		#endregion


		#region properties

		public IPEndPoint EndPoint {
			get {
				return this.endPoint;
			}
			set {
				// argument checks
				if (value == null) {
					throw new ArgumentNullException(nameof(value));
				}

				this.endPoint = value;
			}
		}

		public int Backlog {
			get {
				return this.backlog;
			}
			set {
				// argument checks
				if (value < 0) {
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				this.backlog = value;
			}
		}

		#endregion


		#region creation and disposal

		public ListenerConfiguration(IPEndPoint endPoint, int backlog = DefaultBacklog) {
			// argument checks
			if (endPoint == null) {
				throw new ArgumentNullException(nameof(endPoint));
			}
			if (backlog < 0) {
				throw new ArgumentOutOfRangeException(nameof(backlog));
			}

			// initialize members
			this.endPoint = endPoint;
			this.backlog = backlog;

			return;
		}

		public ListenerConfiguration() {
			// initialize members to default values
			this.endPoint = new IPEndPoint(IPAddress.Loopback, DefaultPort);
			this.backlog = DefaultBacklog;

			return;
		}

		#endregion


		#region methods - ListenerConfiguration

		/// <summary>
		/// 
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		/// <remarks>
		/// IPv6 address should be put in square brackets.
		/// ex. [::1]
		/// </remarks>
		public static ListenerConfiguration Parse(string str) {
			// argument checks
			if (str == null) {
				throw new ArgumentNullException(nameof(str));
			}

			// extract a ListenerConfiguration instance from the string
			ListenerConfiguration instance;
			using (IEnumerator<char> enumerator = str.GetEnumerator()) {
				ScanningAdapter scanner = new ScanningAdapter(enumerator);

				instance = Extract(scanner);	// may be null
				if (instance == null || scanner.HasMoreData) {
					throw CreateFormatException();
				}
			}

			return instance;
		}

		public static ListenerConfiguration[] ParseMultiple(string str) {
			// argument checks
			if (str == null) {
				throw new ArgumentNullException(nameof(str));
			}

			// extract ListenerConfiguration instances from the string
			List<ListenerConfiguration> list = new List<ListenerConfiguration>();
			using (IEnumerator<char> enumerator = str.GetEnumerator()) {
				ScanningAdapter scanner = new ScanningAdapter(enumerator);

				ListenerConfiguration instance;
				while ((instance = Extract(scanner)) != null) {
					list.Add(instance);
				}
			}

			return list.ToArray();
		}

		#endregion


		#region methods - DnsEndPoint

		public static DnsEndPoint ParseDnsEndPoint(string str) {
			// argument checks
			if (str == null) {
				throw new ArgumentNullException(nameof(str));
			}

			// extract host and port
			string host;
			int port;
			using (IEnumerator<char> enumerator = str.GetEnumerator()) {
				ScanningAdapter scanner = new ScanningAdapter(enumerator); 

				if (ExtractEndPoint(scanner, out host, out port) == false || scanner.HasMoreData) {
					throw CreateFormatException();
				}
			}

			// create a DnsEndPoint instance
			try {
				return new DnsEndPoint(host, port);
			} catch (ArgumentException) {
				throw CreateFormatException();
			}
		}

		public static string DnsEndPointToString(DnsEndPoint endPoint) {
			// argument checks
			if (endPoint == null) {
				throw new ArgumentNullException(nameof(endPoint));
			}

			return $"{endPoint.Host}:{endPoint.Port}";
		}

		#endregion


		#region overrides

		public override string ToString() {
			if (this.backlog == DefaultBacklog) {
				return this.endPoint.ToString();
			} else {
				return $"{this.endPoint},backlog={this.backlog}";
			}
		}

		#endregion


		#region privates

		public static ListenerConfiguration Extract(ScanningAdapter scanner) {
			// argument checks
			Debug.Assert(scanner != null);
			string host = null;
			int port = 0;
			int backlog = DefaultBacklog;

			try {
				// extract an end point
				if (ExtractEndPoint(scanner, out host, out port) == false) {
					Debug.Assert(scanner.EndOfData);
					return null;    // not extracted
				}

				// extract parameters
				while (scanner.HasMoreData && scanner.Current == ',') {
					string paramName = null;
					string paramValue = null;

					// skip the separator (',')
					scanner.MoveNext(shouldNotEnd: true);

					// skip whitespaces
					scanner.SkipWhitespaces(shouldNotEnd: true);

					// extract the name
					paramName = scanner.Extract(c => (c == '=' || ScanningAdapter.IsGeneralSeparator(c)), shouldNotEnd: true);

					// skip whitespaces
					scanner.SkipWhitespaces(shouldNotEnd: true);
					if (scanner.Current != '=') {
						throw CreateFormatException();
					}

					// skip '='
					if (scanner.MoveNext()) {
						// skip whitespaces
						if (scanner.SkipWhitespaces()) {
							// extract value
							paramValue = scanner.Extract(ScanningAdapter.IsGeneralSeparator);
							if (scanner.HasMoreData) {
								// skip whitespaces
								scanner.SkipWhitespaces();
							}
						}
					}

					// handle parameters
					if (ParameterNames.AreEqual(paramName, ParameterNames.Backlog)) {
						if (paramValue != null) {
							backlog = int.Parse(paramValue);
							if (backlog < 0) {
								throw CreateFormatException();
							}
						}
					} else {
						// unrecognized parameter
						throw CreateFormatException();
					}
				}

				// end of config
				Debug.Assert(scanner.EndOfData || scanner.Current == ';');
			} catch (EndOfStreamException) {
				// unexpected End Of Data
				throw CreateFormatException();
			}

			try {
				return new ListenerConfiguration(new IPEndPoint(IPAddress.Parse(host), port), backlog);
			} catch (ArgumentException) {
				throw CreateFormatException();
			}
		}

		public static bool ExtractEndPoint(ScanningAdapter scanner, out string host, out int port) {
			// argument checks
			Debug.Assert(scanner != null);
			host = null;
			port = 0;

			try {
				// skip leading whitespaces
				if (scanner.MoveNext() == false || scanner.SkipWhitespaces() == false) {
					// no config
					return false;	// not extracted
				}

				// extract the host part
				if (scanner.Current == '[') {
					// IPv6 address
					// this implementation requires square brackets notation

					// extract before ']'
					scanner.ReadToStockBuffer(c => (c == ']'), shouldNotEnd: true);

					// move to the next (should be ':')
					scanner.ReadToStockBufferAndMoveNext(shouldNotEnd: true);
					if (scanner.Current != ':') {
						throw CreateFormatException();
					}
				} else {
					// IPv4 address or host name

					// extract before ':'
					scanner.ReadToStockBuffer(c => (c == ':'), shouldNotEnd: true);
				}
				host = scanner.ExtractFromSockBuffer();
				if (string.IsNullOrEmpty(host)) {
					throw CreateFormatException();
				}

				// extract the port part
				// skip the separator(':')
				Debug.Assert(scanner.Current == ':');
				scanner.MoveNext(shouldNotEnd: true);

				// extract text
				string value = scanner.Extract(ScanningAdapter.IsGeneralSeparator);	// may be EndOfData
				port = int.Parse(value);

				// skip trailing whitespaces
				if (scanner.HasMoreData) {
					scanner.SkipWhitespaces();
				}
			} catch (EndOfStreamException) {
				// unexpected End Of Data
				throw CreateFormatException();
			}

			return true;	// extracted
		}

		private static Exception CreateFormatException() {
			throw new FormatException();
		}

		#endregion
	}
}
