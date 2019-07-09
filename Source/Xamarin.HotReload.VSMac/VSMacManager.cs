using System;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;

using Mono.Debugging.Soft;

using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Composition;
using MonoDevelop.Ide.Gui.Documents;

using Xamarin.HotReload.Ide;
using static Xamarin.HotReload.LogLevel;

namespace Xamarin.HotReload.VSMac
{
    class VSMacManager
    {
        IdeManager ide;
        IdeManager Ide
            => ide ?? (ide = CompositionManager.Instance.GetExportedValue<IdeManager>());

        static DotNetProject ActiveProject
            => (IdeApp.ProjectOperations.CurrentSelectedSolution?.StartupItem
             ?? IdeApp.ProjectOperations.CurrentSelectedBuildTarget)
             as DotNetProject;

        static VSMacManager instance;

        public static void Init()
        {
            if (instance is null)
                instance = new VSMacManager();
        }

        VSMacManager()
        {
            // Don't call into CompositionManager here, since we're in the start up handler, and MEF may not be available yet

            // Monitor document changes
            XamlDocumentController.DocumentSavedHandler = OnXamlDocumentUpdated;
            CSDocumentController.DocumentSavedHandler = OnCSDocumentUpdated;

            // Monitor debugging session
            IdeApp.ProjectOperations.BeforeStartProject += ProjectOperations_BeforeStartProject;
            MonoDevelop.Debugger.DebuggingService.DebugSessionStarted += DebuggingService_DebugSessionStarted;
            MonoDevelop.Debugger.DebuggingService.StoppedEvent += DebuggingService_StoppedEvent;

            LoggingService.Log(MonoDevelop.Core.Logging.LogLevel.Info, "Hot Reload IDE Extension Loaded");
        }

       

        bool shouldRun;
        void ProjectOperations_BeforeStartProject(object sender, EventArgs e)
        {
            try
            {
                // TODO: Validate hot reload can actually run and if not, display something to user?
                var proj = ActiveProject.FileName;
                var dll = (ActiveProject.DefaultConfiguration as MonoDevelop.Projects.DotNetProjectConfiguration)?.CompiledOutputName;
                shouldRun = RoslynCodeManager.Shared.ShouldHotReload(ActiveProject?.FileName);
            }
            catch(Exception ex)
            {

                LoggingService.Log(MonoDevelop.Core.Logging.LogLevel.Error, $"Hot Reload IDE Extension failed: {ex}");
            }
        }
        bool isSim;
        void DebuggingService_DebugSessionStarted(object sender, EventArgs e)
        {
            if (!Ide.Settings.HotReloadEnabled || !shouldRun)
            {
                LoggingService.Log(MonoDevelop.Core.Logging.LogLevel.Info, "Hot Reload IDE Extension Skipped");
                return;
            }

			var debuggerSession = MonoDevelop.Debugger.DebuggingService.DebuggerSession as SoftDebuggerSession;
            debuggerSession.TargetReady += DebuggerSession_TargetStarted;
			var projectFlavor = ActiveProject?.GetProjectFlavor () ?? ProjectFlavor.None;

            if (!RoslynCodeManager.Shared.ShouldHotReload(ActiveProject?.FileName))
                return;

            if(debuggerSession.GetType().Name == "IPhoneDebuggerSession")
            {
                Console.WriteLine("Is iPhone");
                var simProp = debuggerSession.GetType().GetField("isSimulator", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                var val = simProp.GetValue(debuggerSession);
                Console.WriteLine(val);
            }
            LoggingService.Log(MonoDevelop.Core.Logging.LogLevel.Info, "Hot Reload Debug Session started!");
            Ide.StartHotReload (projectFlavor, debuggerSession);
            RoslynCodeManager.Shared.StartDebugging();
        }

        private void DebuggerSession_TargetStopped(object sender, Mono.Debugging.Client.TargetEventArgs e)
        {

        }

        private void DebuggerSession_TargetStarted(object sender, EventArgs e)
        {
            var debuggerSession = MonoDevelop.Debugger.DebuggingService.DebuggerSession as SoftDebuggerSession;
            if (debuggerSession.GetType().Name == "IPhoneDebuggerSession")
            {
                Console.WriteLine("Is iPhone");
                var simProp = debuggerSession.GetType().GetField("isSimulator", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                var val = simProp.GetValue(debuggerSession);
                isSim = (bool)val;
                simProp.SetValue(debuggerSession, false);
                Console.WriteLine(val);
            }
            debuggerSession.TargetReady -= DebuggerSession_TargetStarted;
        }

        void DebuggingService_StoppedEvent (object sender, EventArgs e)
		{
            if(isSim)
            {
                var debuggerSession = MonoDevelop.Debugger.DebuggingService.DebuggerSession as SoftDebuggerSession;
                var endSession = debuggerSession?.GetType().GetMethod("EndSession", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                endSession?.Invoke(debuggerSession,null);
            }
			Ide.StopHotReload (HotReloadStopReason.ExplicitlyEnded);
            RoslynCodeManager.Shared.StopDebugging();
		}

		void OnXamlDocumentUpdated (FileIdentity fileIdentity)
		{
			if (!Ide.Settings.HotReloadEnabled)
				return;

			Ide.XamlChanged (fileIdentity);
		}

		void OnCSDocumentUpdated (FileIdentity fileIdentity)
		{
			if (!Ide.Settings.HotReloadEnabled || !shouldRun)
				return;

			Ide.CodeFileChanged (fileIdentity);
		}
	}
}
