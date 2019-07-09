using Mono.Debugger.Soft;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.HotReload.Telemetry;
using static Xamarin.HotReload.LogLevel;

namespace Xamarin.HotReload
{
	public class HotReloadBridge : IDisposable
	{
		public const uint BRIDGE_VERSION = 1;

		readonly object queueLocker = new object ();
		Queue<ReloadChange> changeQueue;
		Queue<Message> invokeQueue;

		ITelemetryService telemetry;
		Dictionary<Guid,ITelemetryScope> telemetryScopes; // lock!

		TypeMirror agentType;
		ObjectMirror agentInstance;
		FunctionBreakpoint invokeQueueBreakpoint;
		SoftDebuggerSession debugger;
		FunctionBreakpoint entryBreakpoint;
		readonly ProjectFlavor flavor;
		FunctionBreakpoint messageReceivedBreakpoint;
		ILogger logger;
		string customEntryMethodName;
		string[] customEntryParameterTypes;
		CancellationTokenSource timeoutCancellationTokenSource;

		public uint AgentVersion { get; private set; }

		public Action<ViewAppearedMessage> ViewAppearedHandler { get; set; }
		public Action<ViewDisappearedMessage> ViewDisappearedHandler { get; set; }
		public Action<ReloadTransactionMessage> ReloadResultHandler { get; set; }
		public Action<AgentStatusMessage> AgentStatusChangedHandler { get; set; }
		public Action<FileContentRequest> FileContentRequestHandler { get; set; }

		public HotReloadBridge (ILogger logger, ITelemetryService telemetry, ProjectFlavor flavor, string customEntryMethodName = null, string[] customEntryParameterTypes = null)
		{
			this.logger = logger ?? new DefaultLogger ().WithTag ("HotReload-Bridge");
			this.telemetry = telemetry ?? throw new ArgumentNullException (nameof (telemetry));
			this.flavor = flavor;
			this.customEntryMethodName = customEntryMethodName;
			this.customEntryParameterTypes = customEntryParameterTypes;

			changeQueue = new Queue<ReloadChange> ();
			invokeQueue = new Queue<Message> ();
			telemetryScopes = new Dictionary<Guid,ITelemetryScope> ();
		}

		public void Start (SoftDebuggerSession debugger)
		{
			if (this.debugger != null)
				return;

			// Listen for breakpoints hit
			this.debugger = debugger;
			debugger.TargetHitBreakpoint += Debugger_TargetHitBreakpoint;

			var (method, paramTypes) = GetEntryPointForProjectPlatform (flavor);
			var entryMethod = customEntryMethodName ?? method;
			var entryParamTypes = customEntryParameterTypes ?? paramTypes;

			if (string.IsNullOrEmpty(entryMethod)) {
				logger.Log (Error, "No suitable application entry point method could be found.");
				return;
			}

			entryBreakpoint = new FunctionBreakpoint (entryMethod, "C#");
			if (entryParamTypes != null)
				entryBreakpoint.ParamTypes = entryParamTypes;
			entryBreakpoint.NonUserBreakpoint = true;

			debugger.Breakpoints.Add (entryBreakpoint);
		}

		static (string method, string[] paramTypes) GetEntryPointForProjectPlatform (ProjectFlavor flavor)
		{
			switch (flavor) {
			case ProjectFlavor.Android:
				return ("Android.Runtime.JNIEnv.RegisterJniNatives", new[] { "System.IntPtr", "System.Int32", "System.IntPtr", "System.IntPtr", "System.Int32" });
			case ProjectFlavor.iOS:
				return ("UIKit.UIApplication.Main", new[] { "System.String[]", "System.IntPtr", "System.IntPtr" });
			case ProjectFlavor.Mac:
				return ("AppKit.NSApplication.Main", new[] { "System.String[]" });
			default:
				return default;
			}
		}

		ObjectMirror InjectAndLoadAssembly (SoftEvaluationContext context, TypeMirror systemReflectionAssembly, string assemblyPath)
		{
			var rawAssembly = File.ReadAllBytes (assemblyPath);
			var asmByteCount = rawAssembly?.Length ?? -1;

			logger.Log (Debug, $"Injecting Assembly ({asmByteCount} bytes)...");
			var softAdapter = context.Adapter as SoftDebuggerAdaptor;

			// Create a byte array
			var rawAssemblyByteArray = softAdapter.CreateByteArray (context, rawAssembly);
			// Get our byte[] array ty[e
			var byteArrayType = context.Adapter.GetType (context, "System.Byte[]");
#if hDEBUG
			var rawSymbolStore = File.ReadAllBytes (Path.ChangeExtension (assemblyPath, ".pdb"));
			var rawSymbolStoreByteArray = softAdapter.CreateByteArray (context, rawSymbolStore);
			var argTypes = new object[] { byteArrayType, byteArrayType };
			var argValues = new object[] { rawAssemblyByteArray, rawSymbolStoreByteArray };
#else
			var argTypes = new object[] { byteArrayType };
			var argValues = new object[] { rawAssemblyByteArray };
#endif
			var assemblyObj = (ObjectMirror)context.Adapter.RuntimeInvoke (context, systemReflectionAssembly, null, "Load", argTypes, argValues);

			if (assemblyObj == null)
				throw new NullReferenceException ($"Failed to Load Assembly ({asmByteCount} bytes).");

			logger.Log (Debug, $"Injected Assembly ({asmByteCount} bytes).");

			// Load the assembly byte[] data
			return assemblyObj;
		}

		void InitializeAgent ()
		{
			logger.Log (Debug, "Initializing HotReload Agent...");

			AgentStatusChangedHandler?.Invoke (new AgentStatusMessage { State = HotReloadState.Starting });

			var evalContext = debugger.GetEvaluationContext ();

			// Load System.Reflection.Assembly
			var assemblyType = (TypeMirror)evalContext.Adapter.ForceLoadType (evalContext, "System.Reflection.Assembly");

			var agentAssemblyPath = typeof (HotReloadAgent).Assembly.Location;

			// Inject the assembly into the debug target

			_ = InjectAndLoadAssembly (evalContext, assemblyType, agentAssemblyPath) ??
				throw new NullReferenceException ($"Failed to Inject Assembly: {agentAssemblyPath}");

			// Load the agent type
			agentType = (TypeMirror)evalContext.Adapter.GetType (evalContext, "Xamarin.HotReload.HotReloadAgent") ??
				throw new NullReferenceException ("Failed to resolve HotReloadAgent type.");

			// Load Xamarin.HotReload.Forms and Contracts
			var additionalAssemblyPaths = new List<string> {
				typeof (IHotReloadAgent).Assembly.Location,		//Xamarin.HotReload.Contracts

				//FIXME: Discover these through the project system
				//typeof (Forms.FormsAgent).Assembly.Location		//Xamarin.HotReload.Forms
				typeof (HotUI.HotUIAgent).Assembly.Location		//Xamarin.HotReload.HotUI
			};

			// Load additional dependency assemblies first
			foreach (var addtlAssembly in additionalAssemblyPaths) {
				_ = InjectAndLoadAssembly (evalContext, assemblyType, addtlAssembly) ??
					throw new NullReferenceException ($"Failed to Inject Assembly: {addtlAssembly}");
			}

			var bridgeVersion = (Value)evalContext.Adapter.CreateValue (evalContext, BRIDGE_VERSION);
			var telemetryEnabled = (Value)evalContext.Adapter.CreateValue (evalContext, telemetry.Enabled);

			// We have another method we always want to keep a breakpoint on
			// which will get called on the agent side when it has a message to send back to us
			EnsureMessageReceivedBreakpoint ();

			// Use the static create method on the agent to get an actual instance of the agent
			agentInstance = (ObjectMirror)agentType.InvokeMethod (
				evalContext.Thread,
				agentType.GetMethod ("GetInstance"),
				new Value[] { bridgeVersion, telemetryEnabled });
			if (agentInstance == null)
				throw new NullReferenceException ("Failed to get HotReloadAgent instance.");
			
			logger.Log (Debug, "Initialized HotReload Agent.");
		}

		void Debugger_TargetHitBreakpoint (object sender, TargetEventArgs e)
		{
			// Don't care about empty or non breakpoint hit events
			if ((e?.Backtrace?.FrameCount ?? -1) <= 0)
				return;

			// We need a non-null, enabled, nonuserbreakpoint breakevent to care
			if (e.BreakEvent == null || !e.BreakEvent.Enabled || !e.BreakEvent.NonUserBreakpoint)
				return;

			if (e.BreakEvent == entryBreakpoint) {
				logger.Log (Debug, "entryBreakpoint");
				ThreadPool.QueueUserWorkItem (delegate {
					try {
						// Disable immediately
						entryBreakpoint.Enabled = false;

						// Setup a cancellable timeout in case something goes bad and we don't catch it
						// so we can still report back a Failed agent status after the time out in a worst case
						timeoutCancellationTokenSource = new CancellationTokenSource ();
						Task.Delay (TimeSpan.FromSeconds (120), timeoutCancellationTokenSource.Token)
							.ContinueWith (t => AbortAgent(new TimeoutException()), TaskContinuationOptions.OnlyOnRanToCompletion);

						InitializeAgent ();
						EnsureInvokeBreakpoint ();
					} catch (Exception ex) {
						AbortAgent (ex);
						logger.Log (ex);
					} finally {
						// See if we should remove the breakpoint after
						if (entryBreakpoint != null) {
							try {
								debugger?.Breakpoints?.Remove (entryBreakpoint);
							} catch (Exception ex) {
								logger.Log (ex);
							}
							entryBreakpoint = null;
						}

						debugger?.Continue ();
					}
				});
			} else if (e.BreakEvent == invokeQueueBreakpoint) {
				logger.Log (Debug, "invokeQueueBreakpoint");
				lock (queueLocker) {
					if (changeQueue.Count == 0 && invokeQueue.Count == 0) {
						// If both were empty, disable the breakpoint again until needed
						invokeQueueBreakpoint.Enabled = false;
						debugger?.Continue ();
						return;
					}
				}
				ThreadPool.QueueUserWorkItem (delegate {
					lock (queueLocker) {
						try {
							// Process all the items in the queue
							while (invokeQueue.Count != 0)
								SendToAgent (invokeQueue.Dequeue ());

							if (changeQueue.Count != 0) {
								var list = new List<ReloadTransaction> (changeQueue.Count);
								do {
									list.Add (new ReloadTransaction (changeQueue.Dequeue ()));
								} while (changeQueue.Count != 0);
								SendToAgent (new ReloadTransactionMessage { Transactions = list.ToArray () });
							}
						} catch (Exception ex) {
							logger.Log (ex);
						} finally {
							// Disable the breakpoint until we need again later
							invokeQueueBreakpoint.Enabled = false;
							debugger?.Continue ();
						}
					}
				});
			} else if (e.BreakEvent == messageReceivedBreakpoint) {
				logger.Log (Debug, "messageReceivedBreakpoint");
				ThreadPool.QueueUserWorkItem (delegate {
					try {
						var payload = (e?.Backtrace?.GetFrame (0)?.GetParameters ()?[0]?.GetRawValue () ?? default) as string;

						if (payload is null) {
							logger.Log (Warn, "IDE Received Empty Message");
							return;
						}

						Message msg = default;

						try {
							msg = (Message)Serialization.DeserializeObject (Convert.FromBase64String(payload));
						} catch (Exception ex) {
							logger.Log (ex);
						}

						ProcessIncomingMessage (msg);

					} catch (Exception ex) {
						logger.Log (ex);
					} finally {
						debugger?.Continue ();
					}
				});
			} else {
				var bt = e?.Backtrace?.GetFrame (0)?.FullStackframeText;
				logger.Log (Warn, $"Unknown Breakpoint Hit: {bt}");
			}

		}

		public void UpdateXaml (FileIdentity file)
		{
			lock (queueLocker) {
				changeQueue.Enqueue (new ReloadChange (file));
				if (invokeQueueBreakpoint != null)
					invokeQueueBreakpoint.Enabled = true;
			}
		}

		public void EnqueueMessageToAgent (Message message)
		{
			lock (queueLocker) {
				invokeQueue.Enqueue (message);
				if (invokeQueueBreakpoint != null)
					invokeQueueBreakpoint.Enabled = true;
			}
		}

		void EnsureInvokeBreakpoint ()
		{
			const string breakpointCheckpointMethod = "Xamarin.HotReload.HotReloadAgent.BreakpointCheckpoint";

			EnsureBreakpoint (breakpointCheckpointMethod, ref invokeQueueBreakpoint, createWithEnabled: true);
		}

		void EnsureMessageReceivedBreakpoint ()
		{
			const string breakpointSendToIdeMethod = "Xamarin.HotReload.HotReloadAgent.BreakpointSendToIde";

			EnsureBreakpoint (breakpointSendToIdeMethod, ref messageReceivedBreakpoint, new[] { "System.String" });
		}

		void EnsureBreakpoint(string breakpointMethod, ref FunctionBreakpoint breakpoint, string[] paramTypes = null, bool createWithEnabled = false)
		{
			lock (queueLocker) {
				var debuggerHasBreakpoint = false;

				if (breakpoint == null) {
					breakpoint = new FunctionBreakpoint (breakpointMethod, "C#");
					breakpoint.NonUserBreakpoint = true;
					breakpoint.ParamTypes = paramTypes ?? Array.Empty<string> ();
					if (createWithEnabled)
						breakpoint.Enabled = true;
				} else {
					debuggerHasBreakpoint = debugger?.Breakpoints?.Contains (breakpoint) ?? false;
				}

				if (!debuggerHasBreakpoint)
					debugger?.Breakpoints?.Add (breakpoint);
			}
		}

		void SendToAgent (Message message)
		{
			logger.Log (Debug, $"Sending Message to Agent: {message.GetType().Name}");

			byte [] payload = null;

			try {
				payload = Serialization.SerializeObject (message);
			} catch (Exception ex) {
				logger.Log (ex);
			}

			// Ensure our received message breakpoint is set so we can hear back from the agent
			try {
				EnsureMessageReceivedBreakpoint ();
			} catch (Exception ex) {
				logger.Log (ex);
			}

			try {
				var evalContext = debugger.GetEvaluationContext ();

				var strPayload = Convert.ToBase64String (payload);

				var pValue = (Value)debugger.Adaptor.CreateValue (evalContext, strPayload);

				evalContext.RuntimeInvoke (
					agentType.GetMethod ("SendToAgent", "System.String"),
					agentInstance,
					new Value[] { pValue });

				logger.Log (Debug, $"Sent Message to Agent: {message.GetType().Name}");
			} catch (Exception ex) {
				logger.Log (ex);
			}
		}

		void ProcessIncomingMessage (Message message)
		{
			if (message == null) {
				logger.Log (Warn, $"NULL message from agent, ignoring.");
				return;
			}

			switch (message) {

			case ReloadTransactionMessage reloadResMsg:
				ReloadResultHandler?.Invoke (reloadResMsg);
				break;

			case AgentStatusMessage agentInitMsg:
				switch (agentInitMsg.State) {
				case HotReloadState.Failed:
					AbortAgent (agentInitMsg.Exception); // Something failed, abort!
					break;
				case HotReloadState.Enabled:
					timeoutCancellationTokenSource.Cancel ();
					AgentStatusChangedHandler?.Invoke (agentInitMsg);
					break;
				}
				break;
			case ViewAppearedMessage viewAppearedMsg:
				ViewAppearedHandler?.Invoke (viewAppearedMsg);
				break;

			case ViewDisappearedMessage viewDisappearedMsg:
				ViewDisappearedHandler?.Invoke (viewDisappearedMsg);
				break;

			case FileContentRequest fileContentReqMsg:
				FileContentRequestHandler?.Invoke (fileContentReqMsg);
				break;

			case PostTelemetryMessage ptm:
				lock (telemetryScopes) {
					object correlation = null;
					if (ptm.Correlation.HasValue) {
						if (telemetryScopes.TryGetValue (ptm.Correlation.Value, out var scope))
							correlation = scope.Correlation;
						else
							logger.Log (Warn, $"Couldn't find correlation for telemetry event");
					}
					var eventName = TelemetryEvents.AgentPrefix + ptm.EventName;
					if (ptm.ScopeCorrelation.HasValue) {
						var scope = telemetry.Start (ptm.EventType, eventName, correlation, ptm.Properties);
						telemetryScopes.Add (ptm.ScopeCorrelation.Value, scope);
					} else {
						if (ptm.Exception is null)
							telemetry.Post (ptm.EventType, eventName, ptm.Result, correlation, ptm.Properties);
						else
							telemetry.ReportException (eventName, ptm.Exception, correlation, ptm.Properties);
					}
				}
				break;

			case EndTelemetryScopeMessage etsm:
				lock (telemetryScopes) {
					if (telemetryScopes.TryGetValue (etsm.ScopeCorrelation, out var scope)) {
						telemetryScopes.Remove (etsm.ScopeCorrelation);
						if (!(etsm.Properties is null))
							scope.AddProperties (etsm.Properties);
						scope.End (etsm.Result, etsm.ResultMessage);
					} else {
						logger.Log (Warn, $"Couldn't find telemetry scope to end");
					}
				}
				break;

			default:
				logger.Log (Warn, $"Unknown message from agent: {message}");
				break;
			}
		}

		void AbortAgent (Exception exception)
		{
			AgentStatusChangedHandler?.Invoke (
				new AgentStatusMessage {
					State = HotReloadState.Failed,
					Exception = exception
				});

			CleanupSession (removeTargetHitBreakpoint: false);
		}

		void CleanupSession (bool removeTargetHitBreakpoint = true)
		{
			if (debugger != null) {

				try {
					if (removeTargetHitBreakpoint)
						debugger.TargetHitBreakpoint -= Debugger_TargetHitBreakpoint;
				} catch (Exception ex) {
					logger.Log (ex);
				}

				try {
					if (debugger.Breakpoints != null) {
						if (debugger.Breakpoints.Contains (messageReceivedBreakpoint))
							debugger.Breakpoints.Remove (messageReceivedBreakpoint);
						if (debugger.Breakpoints.Contains (invokeQueueBreakpoint))
							debugger.Breakpoints.Remove (invokeQueueBreakpoint);
					}
				} catch (Exception ex) {
					logger.Log (ex);
				}
			}

			if (timeoutCancellationTokenSource != null) {
				timeoutCancellationTokenSource.Dispose ();
				timeoutCancellationTokenSource = null;
			}
		}

#region IDisposable Support
		bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose (bool disposing)
		{
			if (!disposedValue) {
				if (disposing)
					CleanupSession ();

				disposedValue = true;
			}
		}

		public void Dispose ()
		{
			Dispose (true);
		}
#endregion
	}

}
