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
			Request request = componentFactory.AllocRequest();
			try {
				Response response = componentFactory.AllocResponse();
				try {
					// process each client request
					while (request.Read(owner.RequestInput)) {
						// send the request to the server
						// The request is resent while the owner instructs modifications.
						int repeatCount = 0;
						bool retry = OnCommunicate(owner, repeatCount, request, null);
						if (request.IsConnectMethod && owner.ConnectingToProxy == false) {
							// connecting to the actual server directly
							Response.RespondSimpleError(owner.ResponseOutput, 200, "Connection established");
							tunnelingMode = true;
						} else {
							do {
								request.Write(owner.RequestOutput);
								if (response.Read(owner.ResponseInput, request) == false) {
									// no response from the server
									Exception innerException = new Exception("No response from the server.");
									throw new HttpException(innerException, HttpStatusCode.BadGateway);
								}
								++repeatCount;
								retry = OnCommunicate(owner, repeatCount, request, response);
							} while (retry);
							// send the final response to the client
							response.Write(owner.ResponseOutput);
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
							Response.RespondSimpleError(owner.ResponseOutput, httpError.StatusCode, httpError.Message);
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
				Tunnel(owner, owner.RequestInput, owner.RequestOutput, owner.ResponseInput, owner.ResponseOutput);
			}

			return;
		}

		#endregion


		#region privates

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

		private static void Tunnel(ICommunicationOwner owner, Stream requestInput, Stream requestOutput, Stream responseInput, Stream responseOutput) {
			// argument checks
			Debug.Assert(owner != null);
			Debug.Assert(requestInput != null);
			Debug.Assert(requestOutput != null);
			Debug.Assert(responseInput != null);
			Debug.Assert(responseOutput != null);

			// handle tunneling mode communication
			try {
				// notify the owner
				owner.OnTunnelingStarted(CommunicationSubType.Session);

				// run downstream task on another thread
				Task downstreamTask = Task.Run(() => { Forward(owner, responseInput, responseOutput, CommunicationSubType.DownStream); });
				try {
					// run upstream task
					Forward(owner, requestInput, requestOutput, CommunicationSubType.UpStream);
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

		private static void Forward(ICommunicationOwner owner, Stream input, Stream output, CommunicationSubType type) {
			// argument checks
			Debug.Assert(owner != null);
			Debug.Assert(input != null);
			Debug.Assert(output != null);
			Debug.Assert(type == CommunicationSubType.UpStream || type == CommunicationSubType.DownStream);

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
