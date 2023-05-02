using System;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui.Pads;
namespace Reloadify
{
	public class IDE
	{
		public static IDE Shared { get; set; } = new IDE();
		static ProgressMonitor monitor;

		public static void Init()
		{
			monitor = IdeApp.Workbench.ProgressMonitors.GetOutputProgressMonitor(
				"Comet",
				IconId.Null,
				true,
				true,
				true
			);

			IDEManager.Shared.LogAction = (msg) => monitor?.Log.Write(msg);

			IDEManager.Shared.DataRecieved = Shared.OnDataReceived;
			IDEManager.Shared.OnErrors = async (errors) => {
				await MonoDevelop.Core.Runtime.JoinableTaskFactory.SwitchToMainThreadAsync();
				var ideErrors = IdeServices.TaskService.Errors;
				ideErrors.BeginTaskUpdates();
				ideErrors.ClearByOwner(Shared);
				if (errors?.Count() > 0)
					ideErrors.AddRange(
						errors.Select(x => {
							var line = x.Location.GetLineSpan();
							return new MonoDevelop.Ide.Tasks.TaskListEntry(
								new MonoDevelop.Projects.BuildError(
									x.Location?.SourceTree?.FilePath,
									line.StartLinePosition.Line,
									line.StartLinePosition.Character,
									x.Severity.ToString(),
									x.GetMessage()
								),
								owner: Shared
							);
						})
					);
				ideErrors.EndTaskUpdates();
			};
		}

		void OnDataReceived(object message)
		{
			IDEManager.Shared.Log("Data recieved");
		}
	}
}
