using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.Serialization;


namespace MAPE.Http {
	public class HttpException: Exception {
		#region creation and disposal

		public HttpException(HttpStatusCode statusCode, string message): base(message) {
			// status code is set in this.HResult
			this.HResult = (int)statusCode;
		}

		public HttpException(HttpStatusCode statusCode): this(statusCode, GetDefaultMessage(statusCode)) {
		}

		protected HttpException(SerializationInfo info, StreamingContext context): base(info, context) {
		}

		#endregion


		#region properties

		public int StatusCode {
			get {
				return this.HResult;
			}
		}
		public HttpStatusCode HttpStatusCode {
			get {
				return (HttpStatusCode)this.StatusCode;
			}
		}

		#endregion


		#region properties

		public static string GetDefaultMessage(HttpStatusCode statusCode) {
			switch (statusCode) {
				case HttpStatusCode.BadRequest:
					return "Bad Request";
				default:
					// ToDo: should be improved?
					return statusCode.ToString();
			}
		}

		#endregion
	}
}
