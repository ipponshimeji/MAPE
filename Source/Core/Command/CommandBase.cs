using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using MAPE.Utils;
using MAPE.Configuration;
using MAPE.Server;


namespace MAPE.Command {
    public abstract class CommandBase: IDisposable {
		#region types

		public static class OptionNames {
			#region constants

			public const string ConfigFile = "ConfigFile";

			public const string Help = "Help";

			public const string Save = "Save";

			#endregion
		}

		public class CommandKind {
			#region constants

			public const string RunProxy = "RunProxy";

			public const string ShowUsage = "ShowUsage";

			#endregion
		}

		public class SystemSettingSwitcherBase {
			#region data

			public DnsEndPoint SystemProxy { get; protected set; } = null;

			#endregion


			#region creation and disposal

			public SystemSettingSwitcherBase(Proxy proxy) {
				// inirialize members
				this.SystemProxy = GetSystemProxy();

				return;
			}

			#endregion


			#region overridables

			public virtual bool Switch() {
				return false;	// not switched
			}

			public virtual void Restore() {
				return;
			}

			#endregion


			#region privates

			private static DnsEndPoint GetSystemProxy() {
				IWebProxy proxy = WebRequest.GetSystemWebProxy();
				Func<string, DnsEndPoint> detect = (sample) => {
					Uri sampleUri = new Uri(sample);
					DnsEndPoint value = null;
					if (proxy.IsBypassed(sampleUri) == false) {
						Uri uri = proxy.GetProxy(sampleUri);
						if (uri != sampleUri) {
							value = new DnsEndPoint(uri.Host, uri.Port);
						}
					}
					return value;
				};

				DnsEndPoint endPoint = detect("http://www.google.com/");
				if (endPoint == null) {
					endPoint = detect("http://www.microsoft.com/");
				}

				return endPoint;
			}

			#endregion
		}

		#endregion


		#region data

		protected readonly ComponentFactory ComponentFactory;

		protected ProxyConfiguration ProxyConfiguration { get; private set; } = null;

		public string ConfigurationFilePath { get; protected set; } = null;

		public string Kind { get; protected set; } = CommandKind.RunProxy;

		#endregion


		#region creation and disposal

		public CommandBase(ComponentFactory componentFactory) {
			// argument checks
			if (componentFactory == null) {
				// use standard one
				componentFactory = new ComponentFactory();
			}

			// initialize members
			this.ComponentFactory = componentFactory;

			return;
		}

		public virtual void Dispose() {
			// state checks
			Debug.Assert(this.ProxyConfiguration == null);

			// clear members
			this.Kind = null;
			this.ConfigurationFilePath = null;

			return;
		}

		#endregion


		#region methods
		#endregion


		#region overridables - argument processing

		protected virtual void ProcessArguments(string[] args) {
			// argument checks
			Debug.Assert(args != null);

			// convert arguments into Parameter array
			Parameter[] parameters = (
				from arg in args
				where string.IsNullOrEmpty(arg) == false
				select AssortArgument(arg)
			).ToArray();

			// process arguments in two-phase
			PeekArguments(parameters);
			HandleArguments(parameters);

			return;
		}

		protected virtual Parameter AssortArgument(string arg) {
			// argument checks
			Debug.Assert(string.IsNullOrEmpty(arg) == false);

			// assort the argument into an option or a normal argument
			switch (arg[0]) {
				case '/':
				case '-':
					// an option
					int separatorIndex = arg.IndexOf(':', 1);
					if (0 <= separatorIndex) {
						// "/name:value" form
						Debug.Assert(1 <= separatorIndex);
						return new Parameter(arg.Substring(1, separatorIndex - 1), arg.Substring(separatorIndex + 1));
					} else {
						// "/name" form
						return new Parameter(arg.Substring(1), string.Empty);
					}
				default:
					// a normal argument
					return new Parameter(null, arg);
			}
		}

		protected virtual void PeekArguments(Parameter[] args) {
			// argument checks
			Debug.Assert(args != null);

			// peek each argument
			foreach (Parameter arg in args) {
				if (arg.Name == null) {
					// a normal argument
					PeekArgument(arg.Value);
				} else {
					// an option
					PeekOption(arg);
				}
			}

			// initialize the ProxyConfiguration by the configuration file
			// The config file must be loaded before are arguments are handled in HandleArguments(),
			// because command line arguments override the settings from the config file.
			string configFilePath = this.ConfigurationFilePath;
			if (string.IsNullOrEmpty(configFilePath)) {
				configFilePath = null;	// load default configuration file
			}
			this.ProxyConfiguration.LoadConfiguration(configFilePath);

			return;
		}

		protected virtual bool PeekOption(Parameter option) {
			// handle option
			bool handled = true;
			if (option.IsName(OptionNames.ConfigFile)) {
				this.ConfigurationFilePath = option.Value;
			} else {
				handled = false;
			}

			return handled;
		}

		protected virtual bool PeekArgument(string arg) {
			return false;   // not handled
		}

		protected virtual void HandleArguments(IEnumerable<Parameter> args) {
			// argument checks
			Debug.Assert(args != null);

			// handle each argument
			foreach (Parameter arg in args) {
				if (arg.Name == null) {
					// a normal argument
					HandleArgument(arg.Value);
				} else {
					// an option
					HandleOption(arg);
				}
			}

			return;
		}

		protected virtual bool HandleOption(Parameter option) {
			// state checks
			Debug.Assert(this.ProxyConfiguration != null);

			// handle option
			bool handled = true;
			if (option.IsName(OptionNames.Help) || option.IsName("?")) {
				this.Kind = CommandKind.ShowUsage;
			} else if (option.IsName(OptionNames.ConfigFile)) {
				// ignore, it was already peeked
			} else {
				handled = this.ProxyConfiguration.LoadSetting(option);
			}

			return handled;
		}

		protected virtual bool HandleArgument(string arg) {
			return false;	// not handled
		}

		#endregion


		#region overridables - execution

		public virtual void Run(string[] args) {
			// argument checks
			if (args == null) {
				throw new ArgumentNullException(nameof(args));
			}

			try {
				// state checks
				Debug.Assert(this.ComponentFactory != null);

				// create a ProxyConfiguration
				ProxyConfiguration proxyConfiguration = this.ComponentFactory.CreateProxyConfiguration();
				this.ProxyConfiguration = proxyConfiguration;
				try {
					// process arguments
					ProcessArguments(args);
				} finally {
					this.ProxyConfiguration = null;
				}

				// execute command based on arguments
				Execute(proxyConfiguration);
			} catch (Exception exception) {
				// ToDo: Error Message
				Console.Error.WriteLine(exception.Message);
			}

			return;
		}

		public virtual void Execute(ProxyConfiguration proxyConfiguration) {
			// argument checks
			Debug.Assert(proxyConfiguration != null);

			// execute command according to the command kind 
			switch (this.Kind) {
				case CommandKind.RunProxy:
					RunProxy(proxyConfiguration);
					break;
				case CommandKind.ShowUsage:
					ShowUsage(proxyConfiguration);
					break;
				default:
					throw new Exception($"Internal Error: Unexpected CommandKind {this.Kind}");
			}

			return;
		}

		protected virtual void ShowUsage(ProxyConfiguration proxyConfiguration) {
			// argument checks
			Debug.Assert(proxyConfiguration != null);

			// ToDo: usage
			Console.WriteLine("Usage: ToDo:");
		}

		protected abstract void RunProxy(ProxyConfiguration proxyConfiguration);

		protected virtual SystemSettingSwitcherBase GetSystemSwitcher(Proxy proxy) {
			return new SystemSettingSwitcherBase(proxy);
		}

		#endregion
	}
}
