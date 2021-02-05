using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui.Documents;

namespace Reloadify.VSMac
{

	[ExportDocumentControllerExtension(MimeType = "*", FileExtension = "cs")]
	public class CSharpDocumentController : DocumentControllerExtension
	{
		public async override Task OnSave()
		{
			await base.OnSave();
			var doc = Controller?.Document;
			if (doc != null)
			{
				IDEManager.Shared.Solution = IdeApp.TypeSystemService.Workspace.CurrentSolution;
				IDEManager.Shared.HandleDocumentChanged(new DocumentChangedEventArgs(doc?.FileName, doc.Editor?.Text));
			}
		}
	}
}
