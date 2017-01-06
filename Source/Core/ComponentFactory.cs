using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using MAPE.Utils;
using MAPE.ComponentBase;
using MAPE.Http;
using MAPE.Server;
using MAPE.Command;


namespace MAPE {
    public class ComponentFactory {
		#region types

		public class ConnectionCache: InstanceCache<Connection> {
			#region methods

			public Connection AllocConnection(ConnectionCollection owner) {
				// allocate an instance
				Connection instance = AllocInstance();
				try {
					// activate the instance
					instance.ActivateInstance(owner);
				} catch {
					// do not cache back the instance in error 
					DiscardInstance(instance);
					throw;
				}

				return instance;
			}

			public void ReleaseConnection(Connection instance, bool discardInstance) {
				// argument checks
				if (instance == null) {
					throw new ArgumentNullException(nameof(instance));
				}

				// deactivate the instance and try to cache it
				try {
					instance.DeactivateInstance();
					ReleaseInstance(instance, discardInstance);
				} catch {
					// do not cahce back the instance in error 
					DiscardInstance(instance);
					// continue
				}

				return;
			}

			#endregion


			#region overrides

			protected override Connection CreateInstance() {
				return new Connection();
			}

			#endregion
		}

		public class RequestCache: InstanceCache<Request> {
			#region methods

			public Request AllocRequest(Stream input, Stream output) {
				// allocate an instance
				Request instance = AllocInstance();
				try {
					// activate the instance
					instance.AttachStreams(input, output);
				} catch {
					// do not cache back the instance in error 
					DiscardInstance(instance);
					throw;
				}

				return instance;
			}

			public void ReleaseRequest(Request instance, bool discardInstance) {
				// argument checks
				if (instance == null) {
					throw new ArgumentNullException(nameof(instance));
				}

				// deactivate the instance and try to cache it
				try {
					instance.DetachStreams();
					ReleaseInstance(instance, discardInstance);
				} catch {
					// do not cahce back the instance in error 
					DiscardInstance(instance);
					// continue
				}

				return;
			}

			#endregion


			#region overrides

			protected override Request CreateInstance() {
				return new Request();
			}

			#endregion
		}

		public class ResponseCache: InstanceCache<Response> {
			#region methods

			public Response AllocResponse(Stream input, Stream output) {
				// allocate an instance
				Response instance = AllocInstance();
				try {
					// activate the instance
					instance.AttachStreams(input, output);
				} catch {
					// do not cache back the instance in error 
					DiscardInstance(instance);
					throw;
				}

				return instance;
			}

			public void ReleaseResponse(Response instance, bool discardInstance) {
				// argument checks
				if (instance == null) {
					throw new ArgumentNullException(nameof(instance));
				}

				// deactivate the instance and try to cache it
				try {
					instance.DetachStreams();
					ReleaseInstance(instance, discardInstance);
				} catch {
					// do not cahce back the instance in error 
					DiscardInstance(instance);
					// continue
				}

				return;
			}

			#endregion


			#region overrides

			protected override Response CreateInstance() {
				return new Response();
			}

			#endregion
		}

		public class MemoryBlockCache: InstanceCache<byte[]> {
			#region constants

			public const int MemoryBlockSize = 2 * 1024;    // 2K

			#endregion


			#region methods

			public byte[] AllocMemoryBlock() {
				return AllocInstance();
			}

			public void ReleaseMemoryBlock(byte[] instance) {
				ReleaseInstance(instance, discardInstance: false);
			}

			#endregion


			#region overrides

			protected override byte[] CreateInstance() {
				return new byte[MemoryBlockSize];
			}

			#endregion
		}

		#endregion


		#region constants

		public const int MaxCachedConnectionInstanceCount = 8;

		public const int MaxCachedMemoryBlockCount = 16;

		#endregion


		#region data

		private static readonly ConnectionCache connectionCache = new ConnectionCache() { MaxCachedInstanceCount = MaxCachedConnectionInstanceCount };

		private static readonly RequestCache requestCache = new RequestCache() { MaxCachedInstanceCount = MaxCachedConnectionInstanceCount };

		private static readonly ResponseCache responseCache = new ResponseCache() { MaxCachedInstanceCount = MaxCachedConnectionInstanceCount };

		private static readonly MemoryBlockCache memoryBlockCache = new MemoryBlockCache() { MaxCachedInstanceCount = MaxCachedMemoryBlockCount };

		#endregion


		#region methods

		public virtual RunningProxyState CreateRunningProxyState(CommandBase owner) {
			return new RunningProxyState(owner);
		}

		public virtual Proxy CreateProxy(Settings settings) {
			return new Proxy(this, settings);
		}

		public virtual Listener CreateListener(Proxy owner, Settings settings) {
			return new Listener(owner, settings);
		}

		public virtual ConnectionCollection CreateConnectionCollection(Proxy owner) {
			return new ConnectionCollection(owner);
		}

		public virtual Connection AllocConnection(ConnectionCollection owner) {
			return connectionCache.AllocConnection(owner);
		}

		public virtual void ReleaseConnection(Connection instance, bool discardInstance = false) {
			connectionCache.ReleaseConnection(instance, discardInstance);
		}

		public virtual Request AllocRequest(Stream input, Stream output) {
			return requestCache.AllocRequest(input, output);
		}

		public virtual void ReleaseRequest(Request instance, bool discardInstance = false) {
			requestCache.ReleaseRequest(instance, discardInstance);
		}

		public virtual Response AllocResponse(Stream input, Stream output) {
			return responseCache.AllocResponse(input, output);
		}

		public virtual void ReleaseResponse(Response instance, bool discardInstance = false) {
			responseCache.ReleaseResponse(instance, discardInstance);
		}

		public static byte[] AllocMemoryBlock() {
			return memoryBlockCache.AllocMemoryBlock();
		}

		public static void FreeMemoryBlock(byte[] instance) {
			memoryBlockCache.ReleaseMemoryBlock(instance);
		}

		#endregion
	}
}
