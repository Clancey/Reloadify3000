using System;
using System.Threading.Tasks;
using Mono.Addins;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Composition;
using MonoDevelop.Ide.Gui.Documents;

namespace Xamarin.HotReload.VSMac {

	[ExportDocumentControllerExtension (MimeType = "*", FileExtension = "cs")]
	public class CSDocumentController : DocumentControllerExtension {
		ILogger logger;
		ILogger Logger
			=> logger ?? (logger = CompositionManager.Instance.GetExportedValue<ILogger> ());

		public static Action<FileIdentity> DocumentSavedHandler { get; set; }

		public override async Task OnSave ()
		{
			var doc = Controller?.Document;

			if (doc == null || doc?.FileName.Extension != ".cs") {
				await base.OnSave ();
				return;
			}

			var assemblyName = doc.GetProjectAssemblyName ();
			var sourcePath = doc.FileName.FullPath;
			//var relPath = doc.GetProjectPath ();

			var relPath = (doc?.Owner as MonoDevelop.Projects.SolutionItem).FileName;
			if (string.IsNullOrEmpty (relPath))
				return;

			var currentProject = IdeApp.ProjectOperations.CurrentSelectedProject.DefaultConfiguration as MonoDevelop.Projects.DotNetProjectConfiguration;
			if (currentProject == null)
				return;

			var dll = currentProject.CompiledOutputName;

			var fileIdentity = new FileIdentity (
                assemblyName,
				sourcePath,
				relPath,
				(Controller as FileDocumentController)?.Encoding) {
				CurrentAssemblyLocation = dll,
			};

			try {
				await base.OnSave ();
			} catch (Exception ex) {
				Logger.Log (LogLevel.Warn, $"Unable to get saved document info: {ex}");
			}

			DocumentSavedHandler?.Invoke (fileIdentity);
		}
	}
}
