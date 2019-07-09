using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using MonoDevelop.Ide.Gui.Documents;
using System.Linq;
using MonoDevelop.Ide;

namespace HotUI.Reload.VSMac {

	[ExportDocumentControllerExtension (MimeType = "*", FileExtension = "cs")]
	public class CSharpDocumentController : DocumentControllerExtension {
		public async override Task OnSave ()
		{
			await base.OnSave ();
			var doc = Controller?.Document;
			//if (doc != null) {
			//	IDEManager.Shared.HandleDocumentChanged (new DocumentChangedEventArgs (doc?.FileName, doc?.Editor?.Text));
			//}
			//var comp = await Controller.Document.GetCompilationAsync ();
			//var types = comp.Assembly.TypeNames;
			//var project = Controller.Document.DocumentContext.Project;
			//Console.WriteLine (comp);
			var fileName = (Controller?.Document?.Owner as MonoDevelop.Projects.SolutionItem).FileName;
			if (string.IsNullOrEmpty(fileName))
				return;

			var currentProject = IdeApp.ProjectOperations.CurrentSelectedProject.DefaultConfiguration as MonoDevelop.Projects.DotNetProjectConfiguration;
			if (currentProject == null)
				return;
			var dll = currentProject.CompiledOutputName;
			IDEManager.Shared.HandleDocumentChanged (new DocumentChangedEventArgs (doc?.FileName, doc?.Editor?.Text) {
				ProjectFilePath = fileName,
				CurrentAssembly = dll,
			});
		}
		protected override void OnContentChanged ()
		{
			base.OnContentChanged ();
			//HandledChanges ();
		}
		async void HandledChanges()
		{
			var doc = Controller?.Document;
			if (doc == null)
				return;
			IDEManager.Shared.HandleDocumentChanged (new DocumentChangedEventArgs (doc?.FileName, doc?.Editor?.Text));
		}
	}
}
