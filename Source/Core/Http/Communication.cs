using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

			CommunicateInternal(owner, requestInput, requestOutput, responseInput, responseOutput);
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

			CommunicateInternal(owner, clientStream, serverStream, serverStream, clientStream);
		}

		#endregion


		#region privates

		private static void CommunicateInternal(ICommunicationOwner owner, Stream requestInput, Stream requestOutput, Stream responseInput, Stream responseOutput) {
			// argument checks
			Debug.Assert(owner != null);
			Debug.Assert(requestInput != null);
			Debug.Assert(responseOutput != null);
			Debug.Assert(responseInput != null);
			Debug.Assert(requestOutput != null);

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
						MessageBuffer.Modification[] modifications = owner.GetModifications(repeatCount, request, null);
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
				} catch {
					// ToDo: send error response to client
					byte[] bytes = Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n");
					requestOutput.Write(bytes, 0, bytes.Length);
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
			}

			return;
		}

		private static void Tunnel(ICommunicationOwner owner, Stream requestInput, Stream requestOutput, Stream responseInput, Stream responseOutput) {
			// argument checks
			Debug.Assert(owner != null);
			Debug.Assert(requestInput != null);
			Debug.Assert(requestOutput != null);
			Debug.Assert(responseInput != null);
			Debug.Assert(responseOutput != null);

			Task upTask = Task.Run(() => { Forward(owner, responseInput, responseOutput); });
			Forward(owner, requestInput, requestOutput);
			upTask.Wait(5000);

			return;
		}

		private static void Forward(ICommunicationOwner owner, Stream input, Stream output) {
			// argument checks
			Debug.Assert(owner != null);
			Debug.Assert(input != null);
			Debug.Assert(output != null);

			// forward bytes from the input to the output
			try {
				byte[] buf = ComponentFactory.AllocMemoryBlock();
				try {
					int readCount;
					do {
						readCount = input.Read(buf, 0, buf.Length);
						if (readCount <= 0) {
							// the end of stream
							break;
						}
						output.Write(buf, 0, readCount);
					} while (true);
				} finally {
					ComponentFactory.FreeMemoryBlock(buf);
				}
			} catch (EndOfStreamException) {
				// continue
			} catch (Exception exception) {
				owner.Logger.LogError($"Communication terminated: {exception.Message}");
				owner.OnError(exception);
				// continue
			}

			return;
		}

		#endregion
	}
}
