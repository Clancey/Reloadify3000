using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using Xamarin.HotReload.Ide;
using SystemTask = System.Threading.Tasks.Task;

namespace Xamarin.HotReload.Vsix
{
	[Export (typeof (IErrorListProvider))]
	class VSErrorListProvider : IErrorListProvider
	{
		ErrorListProvider errorListProvider = default;

		ErrorListProvider ErrorList
		{
			get {
				if (errorListProvider == null)
					errorListProvider = new ErrorListProvider (XamarinHotReloadExtensionPackage.Instance);
				return errorListProvider;
			}
		}

		public async SystemTask AddAsync (RudeEdit[] rudeEdits)
		{
			await XamarinHotReloadExtensionPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync ();

			foreach (var re in rudeEdits) {
				var newError = new ErrorTask () {
					ErrorCategory = TaskErrorCategory.Error,
					Category = TaskCategory.BuildCompile,
					Text = re.Message ?? "Unsupported XAML Edit",
					Document = re.File.SourcePath,
					Line = re.LineInfo.LineStart - 1, // Off by 1 bug workaround
					Column = re.LineInfo.LinePositionStart,
					//HierarchyItem = hierarchyItem,
				};

				newError.Navigate += (s, e) => {
					// VS has an off by 1 bug we need to work around for display but then
					// revert workaround just for navigation
					newError.Line++;
					ErrorList.Navigate (newError, new Guid (EnvDTE.Constants.vsViewKindCode));
					newError.Line--;
				};

				ErrorList.Tasks.Add (newError);  // add item
			}
		}

		public async SystemTask ShowAsync ()
		{
			await XamarinHotReloadExtensionPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync ();

			ErrorList.Show ();
		}

		public async SystemTask ClearAsync ()
		{
			await XamarinHotReloadExtensionPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync ();

			ErrorList.Tasks.Clear ();
		}
	}
}
