using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using MonoDevelop.Ide.Tasks;
using Xamarin.HotReload.Ide;

namespace Xamarin.HotReload.VSMac
{
	[Export (typeof (IErrorListProvider))]
	class VSErrorListProvider : IErrorListProvider
	{
		static readonly object locker = new object ();

		List<TaskListEntry> trackedErrors = new List<TaskListEntry> ();

		[Import]
		internal Lazy<JoinableTaskContext> JoinableTaskContext;

		public async Task AddAsync (RudeEdit[] rudeEdits)
		{
			if (!(rudeEdits?.Any () ?? false))
				return;

			await JoinableTaskContext.Value.Factory.SwitchToMainThreadAsync ();

			lock (locker) {
				foreach (var re in rudeEdits) {
					var err = new TaskListEntry (
							new MonoDevelop.Core.FilePath (re.File.SourcePath),
							re.Message,
							re.LineInfo.LinePositionStart,
							re.LineInfo.LineStart,
							TaskSeverity.Error);

					MonoDevelop.Ide.IdeServices.TaskService.Errors.Add (err);
					trackedErrors.Add (err);

				}
			}
		}

		public Task ShowAsync ()
		{
			return Task.CompletedTask;
		}

		public async Task ClearAsync ()
		{
			await JoinableTaskContext.Value.Factory.SwitchToMainThreadAsync ();

			lock (locker) {
				foreach (var t in trackedErrors)
					MonoDevelop.Ide.IdeServices.TaskService.Errors.Remove (t);

				trackedErrors.Clear ();
			}
		}
	}
}
