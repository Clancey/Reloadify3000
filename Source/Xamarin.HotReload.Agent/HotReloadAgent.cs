using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Stopwatch = System.Diagnostics.Stopwatch;

using Xamarin.HotReload;
using Xamarin.HotReload.Telemetry;
using static Xamarin.HotReload.LogLevel;

namespace Xamarin.HotReload
{
	public class HotReloadAgent
	{
		public const uint AGENT_VERSION = 1;

		static readonly object lockObj = new object ();
		static readonly ILogger logger = new DefaultLogger ();

		static ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string> ();
		static HotReloadAgent instance;

		Timer timerBreak;
		List<IHotReloadAgent> agents = new List<IHotReloadAgent> ();

		public uint BridgeVersion { get; }
		public GlobalScopedTelemetryService Telemetry { get; }
		public ILogger RootLogger => logger;

		public static HotReloadAgent GetInstance (uint bridgeVersion, bool telemetryEnabled)
		{
			if (instance == null)
				instance = new HotReloadAgent (bridgeVersion, telemetryEnabled);

			return instance;
		}

		HotReloadAgent (uint bridgeVersion, bool telemetryEnabled)
		{
			BridgeVersion = bridgeVersion;
			Telemetry = new GlobalScopedTelemetryService (telemetryEnabled? new AgentTelemetryService () : TelemetryService.Disabled);

			// Search for and initialize agents
			var agentExceptions = new List<Exception> ();
			foreach (var asm in AppDomain.CurrentDomain.GetAssemblies ()) {
				logger.Log (Debug, "Inspecting: " + asm.FullName);
				var agentType = GetAgentType (asm);
				if (agentType is null)
					continue;

				var agentInitScope = Telemetry.StartGlobal (TelemetryEventType.Operation, TelemetryEvents.Init,
					properties: ("AgentType", agentType.ToString ()));

				string agentName;
				IHotReloadAgent agent;
				try {
					var sw = Stopwatch.StartNew ();
					agent = CreateAgent (agentType);
					agentName = agent.GetName ();
					logger.Log (Perf, $"Created {agentName} in {sw.ElapsedMilliseconds}ms");

					sw.Restart ();
					var provider = new AgentServiceProvider (this, agent);
					agent.InitializeAgent (provider);
					agents.Add (agent);
					logger.Log (Perf, $"Initialized {agentName} in {sw.ElapsedMilliseconds}ms");
				} catch (Exception ex) {
					logger.Log (ex);
					Telemetry.ReportException (TelemetryEvents.Failed, ex, agentInitScope);
					agentInitScope.End (TelemetryResult.Failure, ex.Message);
					agentExceptions.Add (ex.ToSerializable ());
					continue;
				}

				agentInitScope.End (TelemetryResult.Success, agentName);
			}

			// This is ugly but we fire up a timer so that we are constantly calling a managed method
			// That we can predictable set a non-user breakpoint on from the IDE side
			// since we must be on a managed frame when we invoke managed code from the IDE side
			timerBreak = new Timer (TimerBreak_Callback, null, 3000, -1);
			logger.Log (Info, "HotReload: Initialized Agent.");

			if (agents.Count <= 0 && agentExceptions.Count <= 0)
				agentExceptions.Add (new NotSupportedException ("No Hot Reload Agents were initialized successfully."));

			// Send back a success / failure message and any exceptions we encountered
			SendToIde (new AgentStatusMessage {
				Version = AGENT_VERSION,
				State = agentExceptions.Any() ? HotReloadState.Failed : HotReloadState.Enabled,
				Exception = Exceptions.Combine (agentExceptions)
			});
		}

		static Type GetAgentType (Assembly asm)
		{
			var attr = asm.GetCustomAttribute<HotReloadAgentAttribute> ();
			if (attr is null) {
				logger.Log (Debug, "No HotReload Agent Attribute found on Assembly: " + asm.FullName);
				return null;
			}

			var agentType = attr.AgentType;
			if (agentType is null || !typeof (IHotReloadAgent).IsAssignableFrom (agentType)) {
				logger.Log (Warn, $"Found {nameof (HotReloadAgentAttribute)} in {asm.GetName ().Name}, but specified agent type is invalid");
				return null;
			}
			return agentType;
		}

		static IHotReloadAgent CreateAgent (Type agentType)
		{
			var ctor = agentType.GetConstructor (Type.EmptyTypes);
			if (ctor is null) {
				logger.Log (Warn, $"{agentType.FullName} does not have a public constructor that takes no arguments");
				return null;
			}

			return (IHotReloadAgent)ctor.Invoke (null);
		}

		public void Stop ()
		{
			timerBreak.Change (Timeout.Infinite, Timeout.Infinite);
			logger.Log (Debug, "HotReload: Stopped Agent.");
		}

		public void SendToAgent (string payload)
		{
			try {
				messageQueue.Enqueue (payload);
			} catch (Exception ex) {
				logger.Log (ex);
			}
		}

		public const int DEBUG = 0;
		public static void SendToIde (Message message, int priority = 1)
		{
#if DEBUG
			if (priority <= DEBUG)
				return;
#endif
			string payload = null;

			try {
				payload = Convert.ToBase64String (Serialization.SerializeObject (message));
			} catch (Exception ex) {
				logger.Log (ex);
			}

			if (payload is null) {
				logger.Log (Error, "HotReload: Unknown Message, not sending.");
				return;
			}

			logger.Log (Debug, $"HotReload: Sending to IDE -> {message.GetType ().Name}");

			lock (lockObj) {
				BreakpointSendToIde (payload);
			}
		}

		void ProcessIncomingMessage (Message message)
		{
			switch (message) {

			case ReloadTransactionMessage rtm:
				ProcessTransactions (rtm.Transactions)
					.ContinueWith (_ => SendToIde (rtm))
					.LogIfFaulted (logger);
				break;

			case AsyncResponseMessage arm:
				if (!(arm.Exception is null))
					AsyncRequest.TrySetException (arm.RequestId, arm.Exception);
				else
					AsyncRequest.TrySetResult (arm.RequestId, arm.Result);
				break;
			}
		}

		async Task ProcessTransactions (ReloadTransaction [] reqs)
		{
			logger.Log (Debug, $"Received {reqs.Length} reload request(s)...");
			var reloadScope = Telemetry.StartGlobal (TelemetryEventType.UserTask, TelemetryEvents.Reload,
				properties: ("RequestCount", TelemetryValue.CreateMetric (reqs.Length)));

			var unhandled = new HashSet<ReloadTransaction> (reqs);
			foreach (var agent in agents) {
				try {
					// Only pass in changes that haven't already been handled by other agents..
					await agent.ReloadAsync (unhandled);
				} catch (Exception ex) {
					logger.Log (Error, $"Exception in {nameof (IHotReloadAgent.ReloadAsync)} for {agent.GetName ()}: {ex}");
					Telemetry.ReportException (TelemetryEvents.ReloadFault, ex, reloadScope,
						properties: ("AgentType", agent.GetType ().ToString ()));
				}

				var count = unhandled.RemoveWhere (tx => tx.Result.IsChangeTypeSupported);
				logger.Log (Debug, $"{agent.GetName ()} handled {count} reload request(s)");
				if (unhandled.Count == 0)
					break;
			}

			reloadScope.AddProperty ("UnhandledRequestCount", TelemetryValue.CreateMetric (unhandled.Count));
			reloadScope.End (TelemetryResult.None);
		}

		void TimerBreak_Callback (object state)
		{
			try {
				if (!messageQueue.IsEmpty) {
					while (messageQueue.TryDequeue (out var payload)) {
						try {
							var msg = (Message)Serialization.DeserializeObject (Convert.FromBase64String (payload));
							ProcessIncomingMessage (msg);
						} catch (Exception ex) {
							logger.Log (ex);
						}
					}
				}
			} catch (Exception ex) {
				logger.Log (ex);
			}

			try {
				BreakpointCheckpoint ();
			} catch (Exception ex) {
				logger.Log (ex);
			}

			// Restart the timer
			timerBreak.Change (300, -1);
		}

		// This allows us a known spot to set a breakpoint on the IDE side, it gets called repeatedly
		// And gives us a chance to always be setting up a breakpoint
		[MethodImpl (MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
		public static void BreakpointCheckpoint ()
		{
		}

		[MethodImpl (MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
		public static void BreakpointSendToIde (string payload)
		{
		}
	}
}
