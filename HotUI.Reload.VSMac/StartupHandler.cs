using System;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Projects;

namespace HotUI.Reload
{
	public class StartupHandler : CommandHandler
	{
        static DotNetProject ActiveProject
           => (IdeApp.ProjectOperations.CurrentSelectedSolution?.StartupItem
            ?? IdeApp.ProjectOperations.CurrentSelectedBuildTarget)
            as DotNetProject;

        protected override void Run()
		{
			IDE.Init ();
            IdeApp.ProjectOperations.BeforeStartProject += ProjectOperations_BeforeStartProject;
            MonoDevelop.Debugger.DebuggingService.DebugSessionStarted += DebuggingService_DebugSessionStarted;
            MonoDevelop.Debugger.DebuggingService.StoppedEvent += DebuggingService_StoppedEvent;
        }
        bool shouldRun;
        private void ProjectOperations_BeforeStartProject(object sender, EventArgs e)
        {
            try
            {
                var proj = ActiveProject.FileName;
                var dll = (ActiveProject.DefaultConfiguration as MonoDevelop.Projects.DotNetProjectConfiguration)?.CompiledOutputName;
                shouldRun = RoslynCodeManager.Shared.ShouldHotReload(ActiveProject?.FileName);
            }
            catch (Exception ex)
            {

                LoggingService.Log(MonoDevelop.Core.Logging.LogLevel.Error, $"Hot Reload. HotUI IDE Extension failed: {ex}");
            }
        }

        private void DebuggingService_DebugSessionStarted(object sender, EventArgs e)
        {
            if (!shouldRun)
                return;
           IDEManager.Shared.StartMonitoring();
        }

        private void DebuggingService_StoppedEvent(object sender, EventArgs e)
        {
            if (!shouldRun)
                return;
            IDEManager.Shared.StopMonitoring();
        }
    }
}
