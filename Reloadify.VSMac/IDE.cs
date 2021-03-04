using System;
using System.Linq;
using MonoDevelop.Ide;

namespace Reloadify
{

	public class IDE
	{

		public static IDE Shared { get; set; } = new IDE();
		public static void Init()
		{
			IDEManager.Shared.DataRecieved = Shared.OnDataReceived;
			IDEManager.Shared.OnErrors = async (errors) =>
			{
				await MonoDevelop.Core.Runtime.JoinableTaskFactory.SwitchToMainThreadAsync();
				 var ideErrors = IdeServices.TaskService.Errors;
				ideErrors.BeginTaskUpdates();
				ideErrors.ClearByOwner(Shared);
				if(errors?.Count() > 0)
					ideErrors.AddRange(errors.Select(x =>
					{
						var line = x.Location.GetLineSpan();					
						return new MonoDevelop.Ide.Tasks.TaskListEntry(
							new MonoDevelop.Projects.BuildError(x.Location.SourceTree.FilePath,
							 line.StartLinePosition.Line,
							 line.StartLinePosition.Character,
							 x.Severity.ToString(),
							 x.GetMessage()),
							 owner:Shared
						);
					}));
				ideErrors.EndTaskUpdates();
			};
		}
		void DebuggingStarted()
		{
			IDEManager.Shared.StartMonitoring();
		}
		void DebuggingStopped()
		{

			IDEManager.Shared.StartMonitoring();
		}
		void OnDataReceived(object message)
		{
			Console.WriteLine("Data recieved");
		}
	}
}
