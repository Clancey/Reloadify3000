﻿using System;
using System.Threading.Tasks;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using Microsoft.VisualStudio.Text.UI;
using Microsoft.VisualStudio.Text.Editor;
using System.Linq;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Reloadify
{
	public class StartupHandler : CommandHandler
	{
		static DotNetProject ActiveProject
		   => (IdeApp.ProjectOperations.CurrentSelectedSolution?.StartupItem
			?? IdeApp.ProjectOperations.CurrentSelectedBuildTarget)
			as DotNetProject;
		bool enableOnTyping = false;
		protected override void Run()
		{
			IDE.Init();
			IDEManager.Shared.GetActiveDocumentText = GetCurrentDocumentText;
			IdeApp.Workbench.ActiveDocumentChanged += Workbench_ActiveDocumentChanged;
			IdeApp.ProjectOperations.BeforeStartProject += ProjectOperations_BeforeStartProject;
			MonoDevelop.Debugger.DebuggingService.SessionStarted += DebuggingService_DebugSessionStarted;
			MonoDevelop.Debugger.DebuggingService.SessionStopped += DebuggingService_StoppedEvent;

		}

		MonoDevelop.Ide.Gui.Document currentDocument;
		bool editorBound;
		private void Workbench_ActiveDocumentChanged(object sender, MonoDevelop.Ide.Gui.DocumentEventArgs e)
		{
			IDEManager.Shared.Solution = IdeApp.TypeSystemService?.Workspace?.CurrentSolution;
			if (currentDocument == e.Document)
				return;
			if (currentDocument != null)
			{
				if (editorBound)
				{
					if (enableOnTyping)
						currentDocument.TextBuffer.Changed -= TextBuffer_Changed;
					editorBound = false;
				}
			}
			currentDocument = e.Document;
			if (isDebugging)
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
			if (!enableOnTyping)
				return;
			if (currentDocument.TextBuffer != null)
			{
				currentDocument.ContentChanged -= CurrentDocument_ContentChanged;
				currentDocument.TextBuffer.Changed += TextBuffer_Changed;
				editorBound = true;
			}
		}

		private void TextBuffer_Changed(object sender, Microsoft.VisualStudio.Text.TextContentChangedEventArgs e)
		{
			if (enableOnTyping)
				IDEManager.Shared.TextChanged(currentDocument.FilePath);
		}



		bool shouldRun;
		private async void ProjectOperations_BeforeStartProject(object sender, EventArgs e)
		{
			try
			{
				var solution = IDEManager.Shared.Solution = IdeApp.TypeSystemService.Workspace.CurrentSolution;
			}
			catch (Exception ex)
			{

				IDEManager.Shared.Log($"Reloadify Extension failed: {ex}");
			}
		}

		async Task<string> GetCurrentDocumentText(string filePath)
		{
			if (IdeApp.Workbench.ActiveDocument.FilePath != filePath)
				return null;
			return IdeApp.Workbench.ActiveDocument.TextBuffer.CurrentSnapshot.GetText();
		}
		bool isDebugging;
		private async void DebuggingService_DebugSessionStarted(object sender, EventArgs e)
		{
			var activeProject = IdeApp.TypeSystemService.Workspace.CurrentSolution.Projects.FirstOrDefault(x => x.FilePath == ActiveProject.FileName);
			shouldRun = await RoslynCodeManager.Shared.ShouldHotReload(activeProject);
			if (!shouldRun)
				return;
			isDebugging = true;
			IDEManager.Shared.CurrentProjectPath = IdeApp.Workspace.CurrentSelectedSolution.StartupItem.FileName.FullPath;
			IDEManager.Shared.StartMonitoring();
			currentDocument.TextBuffer.Changed += TextBuffer_Changed;
			editorBound = true;
		}

		private void DebuggingService_StoppedEvent(object sender, EventArgs e)
		{
			isDebugging = false;
			IDEManager.Shared.StopMonitoring();
			if (!shouldRun)
				return;
			if (editorBound)
				currentDocument.TextBuffer.Changed -= TextBuffer_Changed;
		}
	}
}
