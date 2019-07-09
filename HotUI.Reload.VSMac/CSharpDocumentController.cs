using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using MonoDevelop.Ide.Gui.Documents;

namespace HotUI.Reload.VSMac {

	[ExportDocumentControllerExtension (MimeType = "*", FileExtension = "cs")]
	public class CSharpDocumentController : DocumentControllerExtension {
		public async override Task OnSave ()
		{
			await base.OnSave ();
			var doc = Controller?.Document;
			if (doc != null) {
				IDEManager.Shared.HandleDocumentChanged (new DocumentChangedEventArgs (doc?.FileName, doc?.Editor?.Text));
			}
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
