using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MAPE.Utils;


namespace MAPE.Http {
	public class Communication {
		#region methods

		public static void Communicate(ICommunicationOwner owner) {
			// argument checks
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}

			// process Http request/response
			bool tunnelingMode = false;
			IHttpComponentFactory componentFactory = owner.ComponentFactory;
			Request request = componentFactory.AllocRequest(owner.RequestIO);
			try {
				Response response = componentFactory.AllocResponse(owner.ResponseIO);
				try {
					// process each client request
					while (request.Read()) {
						// send the request to the server
						// The request is resent while the owner instructs modifications.
						int repeatCount = 0;
						bool resend = OnCommunicate(owner, repeatCount, request, null);
						if (request.IsConnectMethod && owner.ConnectingToProxy == false) {
							// connecting to the actual server directly
							RespondSimpleError(owner, 200, "Connection established");
							tunnelingMode = true;
						} else {
							do {
								request.Write();
								if (response.ReadHeader(request) == false) {
									// no response from the server
									Exception innerException = new Exception("No response from the server.");
									throw new HttpException(innerException, HttpStatusCode.BadGateway);
								}
								++repeatCount;
								resend = OnCommunicate(owner, repeatCount, request, response);
								if (resend) {
									// skip the response body
									response.SkipBody();
									owner.OnResponseProcessed(request, response, resending: true);
								}
							} while (resend);
							// send the final response to the client
							response.Redirect();
							owner.OnResponseProcessed(request, response, resending: false);
							tunnelingMode = (request.IsConnectMethod && response.StatusCode == 200);
						}

						if (tunnelingMode) {
							// move to tunneling mode
							break;
						} else if (response.KeepAliveEnabled == false) {
							// the client connection is not reuseable
							break;
						}
					}
				} catch (EndOfStreamException exception) {
					// an EndOfStreamException means disconnection at an appropriate timing.
					owner.OnError(request, exception);
					// continue
				} catch (Exception exception) {
					// report the exception to the owner
					HttpException httpError = owner.OnError(request, exception);

					// respond an error message to the client if necessary
					// Null httpError means no need to send any error message to the client.
					if (httpError != null) {
						// Note that the connection may be disabled at this point and may cause an exception.
						try {
							RespondSimpleError(owner, httpError.StatusCode, httpError.Message);
						} catch {
							// continue
						}
					}

					// should return immediately
					return;
				} finally {
					componentFactory.ReleaseResponse(response);
				}
			} finally {
				componentFactory.ReleaseRequest(request);
			}

			// process tunneling mode
			if (tunnelingMode) {
				Tunnel(owner);
			}

			return;
		}

		#endregion


		#region privates

		private static void RespondSimpleError(ICommunicationOwner owner, int statusCode, string reasonPhrase) {
			Response.RespondSimpleError(owner.ResponseIO.Output, statusCode, reasonPhrase);
		}

		private static bool OnCommunicate(ICommunicationOwner owner, int repeatCount, Request request, Response response) {
			// argument checks
			Debug.Assert(owner != null);
			Debug.Assert(request != null);
			Debug.Assert((repeatCount == 0 && response == null) || (0 < repeatCount && response != null));

			// preparation
			if (response != null) {
				// on responded from the server

				// clear the modifications for the previous sending
				request.ClearModifications();
			}

			// ask owner to process
			bool needRetry = owner.OnCommunicate(repeatCount, request, response);
			if (needRetry || response == null) {
				// the case that the request will be sent to the server right after this

				if (owner.ConnectingToProxy == false && request.TargetUri != null) {
					// The case that MAPE is connecting to the server directly and
					// its request-target in request-line is absolute-form
					// (non-null TargetUri means its request-target is absolute-form)

					// convert the request-target from absolute-form to origin-form,
					// because absolute-form is the form to be sent to proxies 
					ModifyForDirectConnecting(request);
				}
			}

			return needRetry;
		}

		private static void ModifyForDirectConnecting(Request request) {
			// argument checks
			Debug.Assert(request != null);
			// non-null TargetUri means its request-target is absolute-form
			Uri targetUri = request.TargetUri;
			Debug.Assert(targetUri != null);

			// convert its request-target from absolute-form to origin-form
			Debug.Assert(request.RequestTargetSpan != Span.ZeroToZero);
			request.AddModification(
				request.RequestTargetSpan,
				(modifier) => { modifier.WriteASCIIString(targetUri.PathAndQuery); return true; }
			);

			// overwrite Host field by the contents of the TargetUri
			Span span = request.HostSpan;
			if (span.IsZeroToZero) {
				span = request.EndOfHeaderFields;
			}
			string modifiedHostField = $"Host: {targetUri.Host}:{targetUri.Port}";
			request.AddModification(
				span,
				(modifier) => { modifier.WriteASCIIString(modifiedHostField, appendCRLF: true); return true; }
			);

			return;
		}

		private static void Tunnel(ICommunicationOwner owner) {
			// argument checks
			Debug.Assert(owner != null);

			// handle tunneling mode communication
			try {
				// notify the owner
				owner.OnTunnelingStarted(CommunicationSubType.Session);

				// run downstream task on another thread
				Task downstreamTask = Task.Run(() => { Forward(owner, CommunicationSubType.DownStream); });
				try {
					// run upstream task
					Forward(owner, CommunicationSubType.UpStream);
				} finally {
					downstreamTask.Wait();
				}

				// notify the owner
				owner.OnTunnelingClosing(CommunicationSubType.Session, null);
			} catch (Exception exception) {
				// notify the owner of the exception
				try {
					owner.OnTunnelingClosing(CommunicationSubType.Session, exception);
				} catch {
					// continue
				}
				// continue
			}

			return;
		}

		private static void Forward(ICommunicationOwner owner, CommunicationSubType type) {
			// argument checks
			Debug.Assert(owner != null);
			IMessageIO io;
			switch (type) {
				case CommunicationSubType.UpStream:
					io = owner.RequestIO;
					break;
				case CommunicationSubType.DownStream:
					io = owner.ResponseIO;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(type));			
			}
			Debug.Assert(io != null);

			Stream input = io.Input;
			Debug.Assert(input != null);
			Stream output = io.Output;
			Debug.Assert(output != null);

			// forward bytes from the input to the output
			try {
				// notify the owner
				owner.OnTunnelingStarted(type);

				// forward bytes
				byte[] buf = ComponentFactory.AllocMemoryBlock();
				try {
					do {
						int readCount = input.Read(buf, 0, buf.Length);
						if (readCount <= 0) {
							// the end of the stream
							break;
						}
						output.Write(buf, 0, readCount);
						output.Flush();
					} while (true);
				} catch (EndOfStreamException) {
					// continue
				} finally {
					ComponentFactory.FreeMemoryBlock(buf);
				}

				// notify the owner of its normal closing 
				owner.OnTunnelingClosing(type, null);
			} catch (Exception exception) {
				// notify the owner of the exception
				try {
					owner.OnTunnelingClosing(type, exception);
				} catch {
					// continue
				}
				// continue
			}

			return;
		}

		#endregion
	}
}
