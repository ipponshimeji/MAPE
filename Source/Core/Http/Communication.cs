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

		public static void Communicate(ICommunicationOwner owner, Stream requestInput, Stream requestOutput, Stream responseInput, Stream responseOutput) {
			// argument checks
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}
			if (requestInput == null) {
				throw new ArgumentNullException(nameof(requestInput));
			}
			if (requestOutput == null) {
				throw new ArgumentNullException(nameof(requestOutput));
			}
			if (responseInput == null) {
				throw new ArgumentNullException(nameof(responseInput));
			}
			if (responseOutput == null) {
				throw new ArgumentNullException(nameof(responseOutput));
			}

			// process Http request/response
			bool tunnelingMode = false;
			ComponentFactory componentFactory = owner.ComponentFactory;
			Request request = componentFactory.AllocRequest(requestInput, requestOutput);
			try {
				Response response = componentFactory.AllocResponse(responseInput, responseOutput);
				try {
					// process each client request
					while (request.Read()) {
						// send the request to the server
						// The request is resent while the owner instructs modifications.
						int repeatCount = 0;
						IEnumerable<MessageBuffer.Modification> modifications = owner.OnCommunicate(repeatCount, request, null);
						do {
							request.Write(modifications);
							response.Read(request);
							++repeatCount;
							modifications = owner.OnCommunicate(repeatCount, request, response);
						} while (modifications != null);

						// send the final response to the client
						response.Write();
						if (request.Method == "CONNECT" && response.StatusCode == 200) {
							// move to tunneling mode
							tunnelingMode = true;
							break;
						} else if (response.KeepAliveEnabled == false) {
							// the client connection is not reuseable
							break;
						}
					}
				} catch (EndOfStreamException) {
					// an EndOfStreamException means disconnection at an appropriate timing.
					// continue
				} catch (Exception exception) {
					// report the exception to the owner
					HttpException httpError = owner.OnError(request, exception);

					// respond an error message to the client if necessary
					// Null httpError means no need to send any error message to the client.
					if (httpError != null) {
						// Note that the connection may be disabled at this point and may cause an exception.
						try {
							response.RespondSimpleError(httpError.StatusCode, httpError.Message);
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
				Tunnel(owner, requestInput, requestOutput, responseInput, responseOutput);
			}

			return;
		}

		public static void Communicate(ICommunicationOwner owner, Stream clientStream, Stream serverStream) {
			Communicate(owner, clientStream, serverStream, serverStream, clientStream);
		}

		#endregion


		#region privates

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
