using System;
using Mono.Debugging.Soft;

using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Composition;
using MonoDevelop.Ide.Gui.Documents;

namespace HotUI.Reload {

	public class IDE {

		public static IDE Shared { get; set; } = new IDE ();
		public static void Init()
		{
			IDEManager.Shared.DataRecieved = Shared.OnDataReceived;
			Shared.MonitorEditorChanges ();
			IDEManager.Shared.StartMonitoring ();


			IdeApp.ProjectOperations.BeforeStartProject += ProjectOperations_BeforeStartProject;

			MonoDevelop.Debugger.DebuggingService.DebugSessionStarted += DebuggingService_DebugSessionStarted;
			MonoDevelop.Debugger.DebuggingService.StoppedEvent += DebuggingService_StoppedEvent;

		}
		void MonitorEditorChanges()
		{

		}
		void OnDataReceived(object message)
		{

		}

		static void ProjectOperations_BeforeStartProject (object sender, EventArgs e)
		{
			// TODO: Validate hot reload can actually run and if not, display something to user?
			Console.WriteLine ("Hello");
			
		}

		static void DebuggingService_DebugSessionStarted (object sender, EventArgs e)
		{
			IDEManager.Shared.StartDebugging ();
			//if (!Ide.Settings.HotReloadEnabled)
			//	return;

			//var debuggerSession = MonoDevelop.Debugger.DebuggingService.DebuggerSession as SoftDebuggerSession;

			//var projectFlavor = ActiveProject?.GetProjectFlavor () ?? ProjectFlavor.None;

			//Ide.StartHotReload (projectFlavor, debuggerSession);
		}

		static void DebuggingService_StoppedEvent (object sender, EventArgs e)
		{
			IDEManager.Shared.StopDebugging ();
			//Ide.StopHotReload (HotReloadStopReason.ExplicitlyEnded);
		}
	}
}
