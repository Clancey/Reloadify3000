using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Mono.Debugging.Soft;
using Xamarin.HotReload.Ide;
using Task = System.Threading.Tasks.Task;
using static Xamarin.HotReload.LogLevel;
using System.Reflection;
using Microsoft.VisualStudio.TextTemplating;
using System.Data;

namespace Xamarin.HotReload.Vsix
{
	[Guid (PackageGuidString)]
	[ProvideBindingPath]
	[PackageRegistration (UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideUIContextRule(
        Constants.LoadContextRuleGuidString,
        name: "Xamarin Hot Reload",
        expression: "XamarinXaml",
        termNames: new[] { "XamarinXaml" },
        termValues: new[] { "SolutionHasProjectCapability:XamarinXaml" })]
    [ProvideAutoLoad (Constants.LoadContextRuleGuidString, flags: PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideOptionPage (typeof (HotReloadOptionsPage), "Xamarin", "Hot Reload", 0, 0, true)]
	public sealed partial class XamarinHotReloadExtensionPackage : AsyncPackage, IVsDebuggerEvents, IVsRunningDocTableEvents3
	{
		public static XamarinHotReloadExtensionPackage Instance {
			get; private set;
		}

		public const string PackageGuidString = "4399ad24-7f0f-48c7-8fcc-e6ddc0a15ece";
		public const string OutputWindowGuid = "9E519B60-F0C4-4F48-9125-931451FBDBD1";

		const int iOSDebuggerSessionPropId = 170002;
		const int androidDebuggerSessionPropId = 151001;

		IdeManager ide;

		IVsSolution vsSolution;
		IVsDebugger debuggerService;
		IVsRunningDocumentTable runningDocTable;
		IVsOutputWindowPane debugOutputPane;
		IVsOutputWindowPane generalOutputPane;
		IVsStatusbar statusBar;

		SolutionEvents solutionEvents;

		uint debugEventsCookie = 0;
		uint docTableCookie = 0;
		DBGMODE debugMode = DBGMODE.DBGMODE_Design;
		bool isDebugging = false;
		ProjectFlavor debuggingFlavor = ProjectFlavor.None;

		protected override async Task InitializeAsync (CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			Instance = this;

			await base.InitializeAsync (cancellationToken, progress);

			await JoinableTaskFactory.SwitchToMainThreadAsync (cancellationToken);

			ide = CompositionManager.GetExportedValue<IdeManager> ();

			var dte = (DTE)(await GetServiceAsync (typeof (DTE)));
			vsSolution = (IVsSolution)Package.GetGlobalService (typeof (IVsSolution));

			InitializeGeneralOutputPane ();

			VSLogger.VsWindowPane = generalOutputPane;

			ide.AgentStatusChanged += IdeManager_AgentStatusChanged;
			ide.AgentViewAppeared += IdeManager_AgentViewAppeared;
			ide.AgentViewDisappeared += IdeManager_AgentViewDisappeared;
			ide.AgentReloadResultReceived += IdeManager_AgentXamlResultReceived;

			ide.Logger.Log (Info, "Hot Reload IDE Extension Loaded");

			// NOTE: GetServiceAsync for IVsDebugger failed with "No Such Interface Supported",
			//       which is why we still use the synchronous call that requires main thread access. -sandy
			debuggerService = (IVsDebugger)GetService (typeof (IVsDebugger));
			debuggerService.AdviseDebuggerEvents (this, out debugEventsCookie);

			statusBar = (IVsStatusbar)GetService (typeof (SVsStatusbar));

			solutionEvents = dte.Events.SolutionEvents;
			solutionEvents.Opened += OnSolutionOpened;
			solutionEvents.AfterClosing += OnAfterSolutionClosing;
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				ide.AgentStatusChanged -= IdeManager_AgentStatusChanged;
				ide.AgentViewAppeared -= IdeManager_AgentViewAppeared;
				ide.AgentViewDisappeared -= IdeManager_AgentViewDisappeared;
				ide.AgentReloadResultReceived -= IdeManager_AgentXamlResultReceived;
				ide = null;

				debuggerService?.UnadviseDebuggerEvents (debugEventsCookie);

				if (solutionEvents != null) {
					solutionEvents.Opened -= OnSolutionOpened;
					solutionEvents.AfterClosing -= OnAfterSolutionClosing;
				}

				Cleanup ();

				System.Diagnostics.Trace.Flush ();
			}

			base.Dispose (disposing);
		}

		void OnSolutionOpened () { } // => RefreshHotReloadBridge ();
		void OnAfterSolutionClosing () => Cleanup ();

		void Cleanup (HotReloadStopReason reason = HotReloadStopReason.Cleanup)
		{
			InitializeGeneralOutputPane ();
			VSLogger.VsWindowPane = generalOutputPane;

			ide?.StopHotReload (reason);

			debuggingFlavor = ProjectFlavor.None;

			if (docTableCookie > 0 && runningDocTable != null) {
				runningDocTable.UnadviseRunningDocTableEvents (docTableCookie);
				runningDocTable = null;
				docTableCookie = 0;
			}
		}

		public int OnModeChange (DBGMODE dbgmodeNew)
		{
			var lastDebugMode = debugMode;
			debugMode = dbgmodeNew;

			if (lastDebugMode == DBGMODE.DBGMODE_Break && dbgmodeNew == DBGMODE.DBGMODE_Run)
				return VSConstants.S_OK;

			// Design means the debugger is stopped. The other modes are runtime modes.
			if (dbgmodeNew == DBGMODE.DBGMODE_Design) {
				isDebugging = false;
				Cleanup (HotReloadStopReason.ExplicitlyEnded);
				return VSConstants.S_OK;
			}

			if (dbgmodeNew == DBGMODE.DBGMODE_Break)
				return VSConstants.S_OK;

			if (ide.Settings.HotReloadEnabled) {

				InitializeDebugOutputPane ();
				VSLogger.VsWindowPane = debugOutputPane;

				var proj = GetStartupProject ();
				debuggingFlavor = proj.GetProjectFlavor ();
				var debugger = GetDebuggerSession (proj);

				if (debugger != null) {
					if (runningDocTable == null && docTableCookie <= 0) {
						runningDocTable = (IVsRunningDocumentTable)GetGlobalService (typeof (SVsRunningDocumentTable));
						runningDocTable.AdviseRunningDocTableEvents (this, out docTableCookie);
					}

					ide.StartHotReload (debuggingFlavor, debugger);

					ide.Logger.Log (Debug, "Debugging Started...");

					isDebugging = true;

					ShowOutputPane ();
				}
			}

			return VSConstants.S_OK;
		}

		SoftDebuggerSession GetDebuggerSession (Project proj)
		{
			var projectFlavor = proj.GetProjectFlavor ();

			switch (projectFlavor) {
			case ProjectFlavor.iOS:
				return proj.GetPropertyValue<SoftDebuggerSession> (iOSDebuggerSessionPropId);
			case ProjectFlavor.Android:
				return proj.GetPropertyValue<SoftDebuggerSession> (androidDebuggerSessionPropId);
			}

			return null;
		}

		Project GetStartupProject ()
		{
			try {
				var dte = GetService (typeof (DTE)) as DTE;

				var startupProjNames = (Array)dte.Solution.SolutionBuild.StartupProjects;
				if (startupProjNames == null || startupProjNames.Length < 1)
					return null;

				var startupProjName = (string)startupProjNames.GetValue (0);

				return dte
					.Solution
					.GetProjects ()
					.FirstOrDefault (x => x.UniqueName == startupProjName);
			} catch (Exception e) {
				ide?.Logger?.Log (e);
				return null;
			}
		}

		public int OnAfterFirstDocumentLock (uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;
		public int OnBeforeLastDocumentUnlock (uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;
		public int OnAfterSave (uint docCookie)
		{
			if (!isDebugging || !ide.Settings.HotReloadEnabled)
				return VSConstants.S_OK;

			uint flags;
			uint readLocks;
			uint editLocks;
			string docPath = null;
			var vsHierarchy = default (IVsHierarchy);
			uint itemId = default;
			IntPtr docData = default;

			try {
				runningDocTable?.GetDocumentInfo (docCookie, out flags, out readLocks, out editLocks, out docPath, out vsHierarchy, out itemId, out docData);

				if (!string.IsNullOrEmpty (docPath)) {
					var fileInfo = new FileInfo (docPath);

					if (fileInfo.Exists && fileInfo.Extension.TrimStart ('.').Equals ("xaml", StringComparison.OrdinalIgnoreCase)) {
						var dte = GetService (typeof (DTE)) as DTE;

						var doc = dte.Documents
							   .OfType<Document> ()
							   .FirstOrDefault (x => x != null && x.FullName == docPath);

						var docEncoding = VSHelpers.GetEncoding (docPath);
						
						AssemblyName assemblyName = default;

						var assemblyNameProjectProperty = doc?.ProjectItem?.ContainingProject?.Properties?.Item ("AssemblyName");

						if (assemblyNameProjectProperty != null && assemblyNameProjectProperty.Value != null)
							assemblyName = new AssemblyName (assemblyNameProjectProperty?.Value.ToString ());

						var rootPath = doc.ProjectItem.ContainingProject.AsMsBuildProject ().DirectoryPath;

						if (!string.IsNullOrEmpty (rootPath)) {
							var fileRelPath = docPath;
							if (fileRelPath.StartsWith (rootPath))
								fileRelPath = fileRelPath.Substring (rootPath.Length).TrimStart ('\\', '/');

							// These agents expect / as path separator
							if (debuggingFlavor == ProjectFlavor.Android || debuggingFlavor == ProjectFlavor.iOS || debuggingFlavor == ProjectFlavor.Mac) {
								ide?.Logger.Log (Debug, "Project is Unix based, converting relative path slashes...");
								fileRelPath = fileRelPath.Replace ('\\', '/');
							}

							var textDoc = (TextDocument)doc.Object ("TextDocument");
							var editPoint = textDoc.StartPoint.CreateEditPoint ();
							var xaml = editPoint.GetText (textDoc.EndPoint);

							var file = new FileIdentity (assemblyName, docPath, fileRelPath, docEncoding);
							ide?.XamlChanged (file);
						}
					}
				}
			} catch (Exception ex) {
				ide?.Logger.Log (ex);
			} finally {
				if (docData != IntPtr.Zero)
					Marshal.Release (docData);
			}

			return VSConstants.S_OK;
		}
		public int OnAfterAttributeChange (uint docCookie, uint grfAttribs) => VSConstants.S_OK;
		public int OnBeforeDocumentWindowShow (uint docCookie, int fFirstShow, IVsWindowFrame pFrame) => VSConstants.S_OK;
		public int OnAfterDocumentWindowHide (uint docCookie, IVsWindowFrame pFrame) => VSConstants.S_OK;
		public int OnAfterAttributeChangeEx (uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew) => VSConstants.S_OK;
		public int OnBeforeSave (uint docCookie)
		{
			UpdateStatusBar ("Reloading XAML...", true);

			return VSConstants.S_OK;
		}

		void InitializeDebugOutputPane ()
		{
			if (debugOutputPane != null)
				return;

			debugOutputPane = InitializeOutputPane (VSConstants.GUID_OutWindowDebugPane);
		}

		void InitializeGeneralOutputPane ()
		{
			if (generalOutputPane != null)
				return;

			generalOutputPane = InitializeOutputPane (VSConstants.GUID_OutWindowGeneralPane);
		}

		IVsOutputWindowPane InitializeOutputPane (Guid paneGuid)
		{
			var outWindow = Package.GetGlobalService (typeof (SVsOutputWindow)) as IVsOutputWindow;
			IVsOutputWindowPane pane;
			outWindow.GetPane (ref paneGuid, out pane);
			return pane;
		}

		void IdeManager_AgentViewDisappeared (object sender, ViewDisappearedMessage e)
		{
		}

		void IdeManager_AgentViewAppeared (object sender, ViewAppearedMessage e)
		{
		}

		async void IdeManager_AgentStatusChanged (object sender, AgentStatusMessage e)
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync (CancellationToken.None);

			if (e.State == HotReloadState.Starting)
				UpdateStatusBar ("Hot Reload Initializing...");
			else if (e.State == HotReloadState.Enabled) {
				UpdateStatusBar ("Hot Reload Connected");
			} else if (e.State == HotReloadState.Failed) {
				UpdateStatusBar ("Hot Reload Failed to Initialize!");
			} else if (e.State == HotReloadState.Disabled)
				UpdateStatusBar ("Hot Reload Stopped");
		}

		void IdeManager_AgentXamlResultReceived (object sender, ReloadTransactionMessage msg)
		{
			var rudeEdits = msg.Transactions.SelectMany (txn => txn.Result.RudeEdits).Count ();
			var statusText = (rudeEdits > 0) ? $"XAML Hot Reload Complete ({rudeEdits} unsupported edits)" : "XAML Hot Reload Successful";

			UpdateStatusBar (statusText, false);

			ShowOutputPane ();
		}

		async Task ShowOutputPane ()
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync (CancellationToken.None);

			if (isDebugging)
				debugOutputPane.Activate (); // Brings this pane into view
			else
				generalOutputPane.Activate ();
		}

		async void UpdateStatusBar (string text, bool? animating = default)
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync ();

			// Make sure the status bar is not frozen
			int frozen;

			statusBar.IsFrozen (out frozen);

			if (frozen != 0)
				statusBar.FreezeOutput (0);

			// Set the status bar text and make its display static.
			statusBar.SetText (text);

			// This was causing the text to not update not sure yet the right way to do this
			// so disabling for now
			//if (animating.HasValue)
			//{
			//    // Use the standard Visual Studio icon for building.
			//    object icon = (short)Microsoft.VisualStudio.Shell.Interop.Constants.SBAI_Build;

			//    if (animating.Value)
			//    {
			//        // Display the icon in the Animation region.
			//        statusBar.Animation(1, ref icon);
			//    }
			//    else
			//    {
			//        // Stop the animation.
			//        statusBar.Animation(0, ref icon);
			//    }
			//}


			// Freeze the status bar.
			statusBar.FreezeOutput (1);
		}
	}
}