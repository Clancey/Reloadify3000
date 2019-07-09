using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualStudio.Shell;
using Mono.Debugging.Soft;

using Xamarin.HotReload.HotUI;
using Xamarin.HotReload.Telemetry;
using static Xamarin.HotReload.LogLevel;

namespace Xamarin.HotReload.Ide
{
	using Debugger = System.Diagnostics.Debugger;

	enum HotReloadStopReason
	{
		/// <summary>
		/// We caught an event that indicated the debugging session ended.
		/// </summary>
		ExplicitlyEnded,

		/// <summary>
		/// We ended a previous session because it was left open when a
		///  new session was started.
		/// </summary>
		Cleanup
	}

	[Export, PartCreationPolicy (CreationPolicy.Shared)]
	class IdeManager
	{
		public event EventHandler<AgentStatusMessage> AgentStatusChanged;
		public event EventHandler<ReloadTransactionMessage> AgentReloadResultReceived;
		public event EventHandler<ViewAppearedMessage> AgentViewAppeared;
		public event EventHandler<ViewDisappearedMessage> AgentViewDisappeared;

		public ILogger Logger { get; }

		public ISettingsProvider Settings { get; }

		public IInfoBarProvider InfoBar { get; }

		public IErrorListProvider ErrorList { get; }

		public GlobalScopedTelemetryService Telemetry { get; }

		public ConcurrentDictionary<string, RudeEdit[]> RudeEdits { get; set; } = new ConcurrentDictionary<string, RudeEdit[]> ();

		HotReloadBridge bridge;

		ITelemetryScope sessionScope, startScope;

		string failedInfoBarId = null;

		[ImportingConstructor]
		public IdeManager (ILogger logger, ISettingsProvider settings, IInfoBarProvider infoBarProvider, IErrorListProvider errorListProvider)
		{
			Logger = logger ?? throw new ArgumentNullException (nameof (logger));
			Settings = settings;
			InfoBar = infoBarProvider;
			ErrorList = errorListProvider;

			Telemetry = new GlobalScopedTelemetryService (
				#if DEBUG
					TelemetryService.Disabled
				#else
					Debugger.IsAttached? TelemetryService.Disabled : new VsTelemetryService (Logger).WithPrefix (TelemetryEvents.Prefix)
				#endif
			);
		}

		public void StartHotReload (ProjectFlavor flavor, SoftDebuggerSession debuggerSession)
		{
			if (bridge != null) {
				Logger.Log (Warn, $"{nameof (StartHotReload)} called but session already running");
				StopHotReload (HotReloadStopReason.Cleanup);
			}

			// Start a user task scope that will include everything until StopHotReload
			// FIXME: Add properties from debugger session
			sessionScope = Telemetry.StartGlobal (TelemetryEventType.UserTask, TelemetryEvents.Session,
				properties: ("ProjectFlavor", flavor.ToString ()));

			bridge = new HotReloadBridge (Logger, Telemetry, flavor);
			bridge.AgentStatusChangedHandler = AgentStatusChangedHandler;
			bridge.ReloadResultHandler = ReloadResultHandler;
			bridge.ViewAppearedHandler = ViewAppearedHandler;
			bridge.ViewDisappearedHandler = ViewDisappearedHandler;
			bridge.FileContentRequestHandler = FileContentRequestHandler;

			// Start a new operation that involves just initializing the session
			startScope = Telemetry.StartGlobal (TelemetryEventType.Operation, TelemetryEvents.SessionInit);
			bridge.Start (debuggerSession);
		}

		void FileContentRequestHandler (FileContentRequest req)
		{
			var resp = new AsyncResponseMessage { RequestId = req.RequestId };
			try {
				resp.Result = File.ReadAllBytes (req.File.SourcePath);
			} catch (Exception ex) {
				Logger.Log (ex);
				resp.Exception = ex;
			} finally {
				bridge.EnqueueMessageToAgent (resp);
			}
		}

		async void AgentStatusChangedHandler (AgentStatusMessage message)
		{
			var txt = string.Empty;
			var result = TelemetryResult.None;
			switch (message.State) {
			case HotReloadState.Enabled:
				txt = "Hot Reload Connected & Ready.";
				result = TelemetryResult.Success;
				break;
			case HotReloadState.Disabled:
				txt = "Hot Reload Stopped.";
				break;
			case HotReloadState.Failed:
				txt = "Hot Reload failed to initialize, check the output logs for more information...";
				result = TelemetryResult.Failure;

				// Close an existing info bar
				if (!string.IsNullOrEmpty (failedInfoBarId)) {
					try {
						await InfoBar.CloseAsync (failedInfoBarId);
					} catch (Exception ex) {
						Logger.Log (ex);
					}
				}

				if (!(message.Exception is null)) {
					(string, TelemetryValue) [] props = null;
					//var invalidXfEx = message.Exception.GetException<InvalidXamarinFormsVersionException> ();
					//if (invalidXfEx != null) {
					//	txt = $"Hot Reload failed to initialize: Xamarin.Forms >= {invalidXfEx.MinimumRequiredVersion} is required";
					//	props = new (string, TelemetryValue) [] {
					//		("DetectedFormsVersion", invalidXfEx.DetectedVersion?.ToString ()),
					//		("MinRequiredFormsVersion", invalidXfEx.MinimumRequiredVersion?.ToString ())
					//	};
					//}
					Telemetry.ReportException (TelemetryEvents.SessionFailed, message.Exception, properties: props);
				}

				try {
					failedInfoBarId = await InfoBar.ShowAsync (txt, true, infoBarId: failedInfoBarId);
				} catch (Exception ex) {
					Logger.Log (ex);
				}
				break;
			case HotReloadState.Starting:
				txt = "Hot Reload Initializing...";
				break;
			}

			Logger.Log (Info, txt);

			if (message.State != HotReloadState.Starting) {
				startScope?.End (result, txt);
				startScope = null;
			}

			AgentStatusChanged?.Invoke (this, message);
		}

		async void ReloadResultHandler (ReloadTransactionMessage message)
		{
			// Do some logging based on the result
			foreach (var txn in message.Transactions) {
				if (txn.Result.IsChangeTypeSupported) {
					var rudeEdits = txn.Result.RudeEdits;
					if (rudeEdits.Count > 0) {
						Logger.Log (Info, $"Reloaded: '{txn.Change}', ignoring {rudeEdits.Count} unsupported edits:");
						var rudeEditsByFile = rudeEdits.GroupBy (re => re.File);
						foreach (var file in rudeEditsByFile) {
							Logger.Log (Info, $"\t In {file.Key}:");
							foreach (var ue in file)
								Logger.Log (Info, $"\t\t {ue.LineInfo} - {ue.Message}");
						}
					} else {
						Logger.Log (Info, $"Successfully reloaded: {txn.Change}");
					}
				} else {
					Logger.Log (Warn, $"Unhandled change: {txn.Change}");
				}
			}

			// This is basically last transaction wins for updating rude edits
			foreach (var t in message.Transactions)
				RudeEdits[t.Change.File.SourcePath] = t.Result.RudeEdits.ToArray ();

			await ErrorList.ClearAsync ();
			if (RudeEdits.Any())
				await ErrorList.AddAsync (RudeEdits.SelectMany (r => r.Value).ToArray ());

			AgentReloadResultReceived?.Invoke (this, message);
		}

		void ViewAppearedHandler (ViewAppearedMessage message)
		{
			Logger.Log (Info, $"Page Appeared in app: '{message.File.RelativePath}'");
			AgentViewAppeared?.Invoke (this, message);
		}

		void ViewDisappearedHandler (ViewDisappearedMessage message)
		{
			Logger.Log (Info, $"Page Disappeared in app: '{message.File.RelativePath}'");
			AgentViewDisappeared?.Invoke (this, message);
		}

		public void StopHotReload (HotReloadStopReason reason)
		{
			if (bridge == null)
				return;

			Logger.Log (Info, $"Stopping Hot Reload session due to {reason} ...");

			AgentStatusChanged?.Invoke (this, new AgentStatusMessage { Version = bridge.AgentVersion, State = HotReloadState.Disabled });

			bridge?.Dispose ();
			bridge = null;

			sessionScope?.End (TelemetryResult.None, reason.ToString ());
			sessionScope = null;

			Logger.Log (Info, "Stopped Hot Reload session.");
		}

		public void XamlChanged (FileIdentity file)
		{
			RudeEdit xmlParseRudeEdit = null;

			try {
				// Try to read the XML first to make sure it's valid
				using (var sr = new StreamReader (file.SourcePath)) {
					var _ = XDocument.Load (sr);
				}
			} catch (XmlException xmlEx) {
				xmlParseRudeEdit = new RudeEdit (file, new LineInfo (xmlEx.LineNumber, xmlEx.LinePosition), xmlEx.Message, true);
			} catch (Exception ex) {
				xmlParseRudeEdit = new RudeEdit (file, new LineInfo (), ex.Message, true);
			}

			if (xmlParseRudeEdit != null) {
				// Short circuit sending to the app since we know we failed
				Logger.Log (Info, $"Xaml change is invalid for '{file.RelativePath}' in '{file.AssemblyName}', not sending to app...");

				var res = new ReloadResult (true, new RudeEdit[] { xmlParseRudeEdit });
				var txn = new ReloadTransaction (new ReloadChange (file)) { Result = res };

				ReloadResultHandler (new ReloadTransactionMessage { Transactions = new ReloadTransaction[] { txn } });
			} else {
				// If we could read the XML, it's valid, send it to the agent
				Logger.Log (Info, $"Xaml Changed for '{file.RelativePath}' in '{file.AssemblyName}', sending to app...");
				bridge?.UpdateXaml (file);
			}
		}

		public void CodeFileChanged (FileIdentity file)
		{
			Exception xmlParseRudeEdit = null;
			try {
				RoslynCodeManager.Shared.HandleFileChange (file);
			} catch (Exception ex) {
				xmlParseRudeEdit = ex;
			}

			if (xmlParseRudeEdit != null) {
				// Short circuit sending to the app since we know we failed
				Logger.Log (Info, $"Xaml change is invalid for '{file.RelativePath}' in '{file.AssemblyName}', not sending to app...");
				//TODO: Send Error
				//var res = new ReloadResult (true, new RudeEdit [] { xmlParseRudeEdit });
				//var txn = new ReloadTransaction (new ReloadChange (file)) { Result = res };

				//ReloadResultHandler (new ReloadTransactionMessage { Transactions = new ReloadTransaction [] { txn } });
			} else {
				// If we could read the XML, it's valid, send it to the agent
				Logger.Log (Info, $"Xaml Changed for '{file.RelativePath}' in '{file.AssemblyName}', sending to app...");
				bridge?.UpdateXaml (file);
			}
		}
	}
}
