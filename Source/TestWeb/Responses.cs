using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

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

			if ((response.Headers.TransferEncodingChunked ?? false) == false && response.Content != null) {
				// ensure that the Content-Length header is added 
				// It seems that Content-Length header is added when ContentLength property is accessed first time.
				long? length = response.Content.Headers.ContentLength;
			}

			return response;
		}

		public static HttpResponseMessage GetNotFoundResponse(string path) {
			HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound);

			return response;
		}

		public static HttpResponseMessage GetSimpleResponse(string text) {
			HttpResponseMessage response = new HttpResponseMessage();
			response.Content = new StringContent(text);			

			return response;
		}

		#endregion
	}
}
