using System;
using System.Threading.Tasks;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using Microsoft.VisualStudio.Text.UI;
using Microsoft.VisualStudio.Text.Editor;

namespace HotUI.Reload
{
	public class StartupHandler : CommandHandler
	{
        static DotNetProject ActiveProject
           => (IdeApp.ProjectOperations.CurrentSelectedSolution?.StartupItem
            ?? IdeApp.ProjectOperations.CurrentSelectedBuildTarget)
            as DotNetProject;

        protected override void Run()
		{
			IDE.Init ();
            IDEManager.Shared.GetActiveDocumentText = GetCurrentDocumentText;
            IdeApp.Workbench.ActiveDocumentChanged += Workbench_ActiveDocumentChanged;
            
            IdeApp.ProjectOperations.BeforeStartProject += ProjectOperations_BeforeStartProject;
            MonoDevelop.Debugger.DebuggingService.DebugSessionStarted += DebuggingService_DebugSessionStarted;
            MonoDevelop.Debugger.DebuggingService.StoppedEvent += DebuggingService_StoppedEvent;

        }
        MonoDevelop.Ide.Gui.Document currentDocument;
        bool editorBound;
        private void Workbench_ActiveDocumentChanged(object sender, MonoDevelop.Ide.Gui.DocumentEventArgs e)
        {
            if (currentDocument == e.Document)
                return;
            if(currentDocument != null)
            {
                if (editorBound)
                {
                    currentDocument.TextBuffer.Changed -= TextBuffer_Changed;
                    editorBound = false;
                }
            }
            currentDocument = e.Document;
            if(isDebugging)
            {
                if (currentDocument.TextBuffer == null)
                {
                    currentDocument.ContentChanged += CurrentDocument_ContentChanged;
                }
                else
                {
                    currentDocument.TextBuffer.Changed += TextBuffer_Changed;
                    editorBound = true;
                }
            }
        }

        private void CurrentDocument_ContentChanged(object sender, EventArgs e)
        {
            if(currentDocument.TextBuffer != null)
            {
                currentDocument.ContentChanged -= CurrentDocument_ContentChanged;
                currentDocument.TextBuffer.Changed += TextBuffer_Changed;
                editorBound = true;
            }
        }

        private void TextBuffer_Changed(object sender, Microsoft.VisualStudio.Text.TextContentChangedEventArgs e)
        {
            IDEManager.Shared.TextChanged(currentDocument.FilePath);
        }

        

        bool shouldRun;
        private void ProjectOperations_BeforeStartProject(object sender, EventArgs e)
        {
            try
            {
                var proj = ActiveProject.FileName;
                var dll = (ActiveProject.DefaultConfiguration as MonoDevelop.Projects.DotNetProjectConfiguration)?.CompiledOutputName;
                shouldRun = RoslynCodeManager.Shared.ShouldHotReload(ActiveProject?.FileName);
            }
            catch (Exception ex)
            {

                LoggingService.Log(MonoDevelop.Core.Logging.LogLevel.Error, $"Hot Reload. HotUI IDE Extension failed: {ex}");
            }
        }

        async Task<string> GetCurrentDocumentText(string filePath)
        {
            if (IdeApp.Workbench.ActiveDocument.FilePath != filePath)
                return null;
            return IdeApp.Workbench.ActiveDocument.TextBuffer.CurrentSnapshot.GetText();
        }
        bool isDebugging;
        private void DebuggingService_DebugSessionStarted(object sender, EventArgs e)
        {
            if (!shouldRun)
                return;
            isDebugging = true;
            IDEManager.Shared.StartMonitoring();
            currentDocument.TextBuffer.Changed += TextBuffer_Changed;
            editorBound = true;
        }

        private void DebuggingService_StoppedEvent(object sender, EventArgs e)
        {
            isDebugging = false;
            if (!shouldRun)
                return;
            IDEManager.Shared.StopMonitoring();
            if(editorBound)
                currentDocument.TextBuffer.Changed -= TextBuffer_Changed;
        }
    }
}
