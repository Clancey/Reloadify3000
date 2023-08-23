using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;
using Reloadify.Hosted;

namespace Reloadify
{
	public class ReloadifyManager
	{
		public Action<string> OnLog{ get; set; }
		public Action<string> CurrentProjectPathChanged { get; set; }
		public Action<Solution> CurrentSolutionChanged { get; set; }
		public Action<Exception> OnError { get; set; }
		public Action OnHotReloadStarted { get; set; }
		public Action OnHotReloadStopped { get; set; }

		public bool ShouldUseFileWatcher{ get; set; } = true;

		public ReloadifyManager()
		{
			IDEManager.Shared.LogAction = (s) => Console.WriteLine(s);
		}
		//MSBuildWorkspace currentWorkSpace;
		public static ReloadifyManager Shared { get; set; } = new ReloadifyManager();
		Project currentProject;
		string csprojPath;
		string projectRoot;
		public async Task LoadProject(string projectRoot, string csprojPath, string configuration, string platform)
		{
			this.projectRoot = projectRoot;
			if (!File.Exists(csprojPath))
			{
				var combined = Path.Combine(projectRoot, csprojPath);
				if (File.Exists(combined))
					csprojPath = combined;
			}
			this.csprojPath = csprojPath;
			try
			{

				MSBuildLocator.RegisterDefaults();
				IDEManager.Shared.OnErrors = OnErrors;
				var currentWorkSpace = MSBuildWorkspace.Create(new Dictionary<string,string>() {
					["Configuration"] = configuration,
					["Platform"] = platform,
				});
				currentWorkSpace.WorkspaceFailed += CurrentWorkSpace_WorkspaceFailed;

				Console.WriteLine($"Opening :{csprojPath}");
				var project = await currentWorkSpace.OpenProjectAsync(csprojPath);
				var sln = currentWorkSpace.CurrentSolution;
				
				currentProject = project;
				IDEManager.Shared.CurrentProjectPath = csprojPath;
				CurrentProjectPathChanged?.Invoke(csprojPath);
				IDEManager.Shared.Solution = sln;
				CurrentSolutionChanged?.Invoke(sln);
							}
			catch (Exception ex)
			{
				OnError?.Invoke(ex);
				Console.WriteLine(ex);
			}

		}

		void OnErrors(IEnumerable<Diagnostic> diagnostics)
		{
			if (!(diagnostics?.Any() ?? false))
			{
				Console.WriteLine("Building new Diff was Successful!");
				return;
			}
			foreach(var e in diagnostics)
				Console.WriteLine(e.GetMessage());
		}
		bool isDebugging;
		private FileWatcher fileWatcher;

		public async Task<bool> StartHotReload()
		{
			var shouldHotReload = await RoslynCodeManager.Shared.ShouldHotReload(currentProject);
			if (!shouldHotReload)
				return false;
			if (isDebugging)
				return true;
			isDebugging = true;
			IDEManager.Shared.CurrentProjectPath = csprojPath;
			if(ShouldUseFileWatcher)
				fileWatcher = new FileWatcher(this,projectRoot);
			IDEManager.Shared.StartMonitoring();
			return true;
		}

		public void Shutdown()
		{
			if (isDebugging)
				return;
			isDebugging = false;
			fileWatcher?.Dispose();
			IDEManager.Shared.StopMonitoring();
		}



		private void CurrentWorkSpace_WorkspaceFailed(object sender, WorkspaceDiagnosticEventArgs e)
		{
			if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure && !e.Diagnostic.Message.Contains("cannot be imported again."))
				Console.WriteLine(e.Diagnostic);
		}

		public void OnFileChanged(string filePath)
		{

		}
	}
}
