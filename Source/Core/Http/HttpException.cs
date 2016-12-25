using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.Serialization;


namespace MAPE.Http {
	public class HttpException: Exception {
		#region data

		private readonly HttpStatusCode httpStatusCode;

		#endregion


		#region properties

		public HttpStatusCode HttpStatusCode {
			get {
				return this.httpStatusCode;
			}
		}

		public int StatusCode {
			get {
				return (int)this.httpStatusCode;
			}
		}

		#endregion


		#region creation and disposal

		public HttpException(HttpStatusCode httpStatusCode, string message): base(message) {
			this.httpStatusCode = httpStatusCode;
		}

		public HttpException(HttpStatusCode httpStatusCode): this(httpStatusCode, GetDefaultMessage(httpStatusCode)) {
		}

		public HttpException(Exception innerException): base(GetDefaultMessage(HttpStatusCode.InternalServerError), innerException) {
			this.httpStatusCode = HttpStatusCode.InternalServerError;
		}

		protected HttpException(SerializationInfo info, StreamingContext context): base(info, context) {
			// ToDo: implement
		}

		#endregion


		#region methods

		public static string GetDefaultMessage(HttpStatusCode statusCode) {
			switch (statusCode) {
				case HttpStatusCode.BadRequest:
					return "Bad Request";
				case HttpStatusCode.InternalServerError:
					return "Internal Server Error";
				default:
					// ToDo: should be improved?
					return statusCode.ToString();
			}
		}

		#endregion
	}
}
