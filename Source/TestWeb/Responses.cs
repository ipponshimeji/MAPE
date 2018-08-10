using System;
using System.Net;
using System.Net.Http;

namespace MAPE.Test.TestWeb {
    public class Responses {
		#region constants

		// paths
		public const string SimplePath = "/simple";
		public const string BasicAuthenticationPath = "/authentication/basic";

		#endregion


		#region methods

		public static HttpResponseMessage GetResponse(string path) {
			HttpResponseMessage response;
			switch (path) {
				case SimplePath:
					response = GetSimpleResponse("<HTML><BODY>Hello World!</BODY></HTML>");
					break;
				default:
					response = GetNotFoundResponse(path);
					break;
			}

			return response;
		}

		#endregion


		#region privates

		public static HttpResponseMessage GetNotFoundResponse(string path) {
			HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound);

			return response;
		}

		public static HttpResponseMessage GetSimpleResponse(string content) {
			HttpResponseMessage response = new HttpResponseMessage();
			response.Content = new StringContent(content);

			return response;
		}

		#endregion
	}
}
