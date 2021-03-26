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

namespace Reloadify.CommandLine
{
	public class IDE
	{
		//MSBuildWorkspace currentWorkSpace;
		public static IDE Shared { get; set; } = new IDE();
		Project currentProject;
		string csprojPath;
		FileWatcher fileWatcher;
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
				var currentWorkSpace = MSBuildWorkspace.Create(new Dictionary<string,string>() {
					["Configuration"] = configuration,
					["Platform"] = platform,
				});
				currentWorkSpace.WorkspaceFailed += CurrentWorkSpace_WorkspaceFailed;
				//var sln = await currentWorkSpace.OpenSolutionAsync(slnPath);
				//var project = sln.Projects.FirstOrDefault(x => x.FilePath.Contains(csp rojPath)) ?? await currentWorkSpace.OpenProjectAsync(csprojPath);
				Console.WriteLine($"Opening :{csprojPath}");
				var project = await currentWorkSpace.OpenProjectAsync(csprojPath);
				var sln = currentWorkSpace.CurrentSolution;
				Console.WriteLine($"Compiling :{csprojPath}");
				var compilation = await project.GetCompilationAsync();
				var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
				Console.WriteLine($"Compiling Complete : Error Count: {errors?.Count() ?? 0}");
				foreach (var e in errors)
					Console.WriteLine($"\t: {e.GetMessage()}");
				currentProject = project;
				IDEManager.Shared.CurrentProjectPath = csprojPath;
				IDEManager.Shared.Solution = sln;
				//var sln = project.Solution;
				//var graph = sln.GetProjectDependencyGraph();
				//Msbuild failed when processing the file '/Users/clancey/Projects/Comet/src/Comet/Comet.csproj' with message: The SDK 'Microsoft.NET.Sdk' specified could not be found.  / Users / clancey / Projects / Comet / src / Comet / Comet.csproj                    IDEManager.Shared.Solution = sln;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}

		}
		bool isDebugging;
		public async Task<bool> StartHotReload()
		{
			var shouldHotReload = await RoslynCodeManager.Shared.ShouldHotReload(currentProject);
			if (!shouldHotReload)
				return false;
			if (isDebugging)
				return true;
			isDebugging = true;
			IDEManager.Shared.CurrentProjectPath = csprojPath;
			fileWatcher = new FileWatcher(projectRoot);
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
			Console.WriteLine(e.Diagnostic);
		}
	}
}
