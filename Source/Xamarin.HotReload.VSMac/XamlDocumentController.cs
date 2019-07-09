using System;
using System.Threading.Tasks;
using Mono.Addins;
using MonoDevelop.Ide.Composition;
using MonoDevelop.Ide.Gui.Documents;

namespace Xamarin.HotReload.VSMac
{
	[ExportDocumentControllerExtension(MimeType = "*", FileExtension = "xaml")]
	public class XamlDocumentController : DocumentControllerExtension
	{
		ILogger logger;
		ILogger Logger
			=> logger ?? (logger = CompositionManager.Instance.GetExportedValue<ILogger> ());

		public static Action<FileIdentity> DocumentSavedHandler { get; set; }

		public override async Task OnSave ()
		{
			var doc = Controller?.Document;

			if (doc == null || doc?.FileName.Extension != ".xaml") {
				await base.OnSave ();
				return;
			}
            

			var assemblyName = doc.GetProjectAssemblyName ();
			var sourcePath = doc.FileName.FullPath;
            
			var relPath = doc.GetProjectPath ();

			var fileIdentity = new FileIdentity (
				doc.GetProjectAssemblyName (),
				doc.FileName.FullPath,
				doc.GetProjectPath (),
				(Controller as FileDocumentController)?.Encoding);

			try {
				await base.OnSave ();
			} catch (Exception ex) {
				Logger.Log (LogLevel.Warn, $"Unable to get saved document info: {ex}");
			}

			DocumentSavedHandler?.Invoke (fileIdentity);
		}
	}
}
