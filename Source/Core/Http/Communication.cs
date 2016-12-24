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
					// process one turn
					while (request.Read()) {
						int repeatCount = 0;
						IEnumerable<MessageBuffer.Modification> modifications = owner.GetModifications(repeatCount, request, null);
						do {
							request.Write(modifications);
							response.Read();
							++repeatCount;
							modifications = owner.GetModifications(repeatCount, request, response);
						} while (modifications != null);
						response.Write();
						if (request.Method == "CONNECT" && response.StatusCode == 200) {
							// move to tunneling mode
							tunnelingMode = true;
							break;
						}
					}
				} catch (HttpException exception) {
					response.RespondSimpleError(exception.StatusCode, exception.Message);
					owner.Logger.LogError($"Responded Error {exception.StatusCode}.");
				} catch (Exception) {
					response.RespondSimpleError((int)HttpStatusCode.InternalServerError, "Internal Server Error");
					owner.Logger.LogError($"Responded Error {(int)HttpStatusCode.InternalServerError}.");
				} finally {
					componentFactory.ReleaseResponse(response);
				}
			} finally {
				componentFactory.ReleaseRequest(request);
			}

			// process tunneling mode
			if (tunnelingMode) {
				owner.Logger.LogInformation("Move to tunneling mode.");
				Tunnel(owner, requestInput, requestOutput, responseInput, responseOutput);
				owner.Logger.LogInformation("Complete tunneling mode.");
			}

			return;
		}

		public static void Communicate(ICommunicationOwner owner, Stream clientStream, Stream serverStream) {
			// argument checks
			if (owner == null) {
				throw new ArgumentNullException(nameof(owner));
			}
			if (clientStream == null) {
				throw new ArgumentNullException(nameof(clientStream));
			}
			if (serverStream == null) {
				throw new ArgumentNullException(nameof(serverStream));
			}

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

			// run downstream task on another thread
			Task upTask = Task.Run(() => { Forward(owner, responseInput, responseOutput, true); owner.Logger.LogWarning("downstream"); });

			// run upstream task
			Forward(owner, requestInput, requestOutput, false);
			owner.Logger.LogWarning("upstream");

			// join the download task
			upTask.Wait();

			return;
		}

		private static void Forward(ICommunicationOwner owner, Stream input, Stream output, bool downstream) {
			// argument checks
			Debug.Assert(owner != null);
			Debug.Assert(input != null);
			Debug.Assert(output != null);

			// forward bytes from the input to the output
			Exception error = null;
			byte[] buf = ComponentFactory.AllocMemoryBlock();
			try {
				do {
					int readCount = input.Read(buf, 0, buf.Length);
					if (readCount <= 0) {
						// the end of stream
						break;
					}
					output.Write(buf, 0, readCount);
					output.Flush();
				} while (true);
			} catch (EndOfStreamException) {
				// continue
			} catch (Exception exception) {
				error = exception;
				owner.Logger.LogError($"Communication terminated: {exception.Message}");
				// continue
			} finally {
				ComponentFactory.FreeMemoryBlock(buf);
			}

			// notify the owner of the closing 
			owner.OnClose(downstream, error);

			return;
		}

		#endregion
	}
}
