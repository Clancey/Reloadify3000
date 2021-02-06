using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Reloadify.Internal;

namespace Reloadify {
	public class RoslynCodeManager {
		public static RoslynCodeManager Shared { get; set; } = new RoslynCodeManager ();

		Dictionary<string, List<string>> referencesForProjects = new Dictionary<string, List<string>> ();
		public bool ShouldHotReload (string project)
		{
			if (string.IsNullOrWhiteSpace (project))
				return false;
			var hasReloadify = File.ReadAllText (project).Contains ("Reloadify3000");
			return hasReloadify;
		}
		public void StartDebugging ()
		{
		}
		public void StopDebugging ()
		{
			referencesForProjects.Clear ();
		}

		public List<string> GetReferences (string projectPath, string currentReference)
		{
			if (referencesForProjects.TryGetValue (projectPath, out var references))
				return references;
			var project = new ProjectInstance (projectPath);
			var result = BuildManager.DefaultBuildManager.Build (
				new BuildParameters (),
				new BuildRequestData (project, new []
			{
				"ResolveProjectReferences",
				"ResolveAssemblyReferences"
			}));

			IEnumerable<string> GetResultItems (string targetName)
			{
				var buildResult = result.ResultsByTarget [targetName];
				var buildResultItems = buildResult.Items;

				return buildResultItems.Select (item => item.ItemSpec);
			}

			references = GetResultItems ("ResolveProjectReferences")
				.Concat (GetResultItems ("ResolveAssemblyReferences")).Distinct ().ToList ();
			if (!string.IsNullOrWhiteSpace (currentReference))
				references.Add (currentReference);
			referencesForProjects [projectPath] = references;
			return references;
		}

		public static async System.Threading.Tasks.Task<EvalRequestMessage> SearchForPartialClasses(string filePath, string fileContents,string projectPath, Microsoft.CodeAnalysis.Solution solution)
		{
			try
			{
				var workspace = MSBuildWorkspace.Create();
				//workspace.WorkspaceFailed += (s,e)=>
				//{
				//	Console.WriteLine(e);
				//};
				//if (workspace?.CurrentSolution?.FilePath != solutionPath)
				//	await workspace.OpenSolutionAsync(solutionPath);


				var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(fileContents);

				var root = tree.GetCompilationUnitRoot();
				var collector = new ClassCollector();
				collector.Visit(root);
				var classes = collector.Classes.Select(x => x.GetClassNameWithNamespace()).ToList();
				if (classes.Count == 0)
					return null;
				var partialClasses = collector.PartialClasses.Select(x => x.GetClassNameWithNamespace()).ToList();
				//collector.Classes.Where(x=> x.)

				var projects = solution.Projects.ToList();
				var activeProject = projects?.FirstOrDefault(x => x.FilePath == projectPath);
				var references = activeProject.ProjectReferences.Select(x=> x.ProjectId).ToList();
				var referencedProjects = projects.Where(x => references.Any(y => y == x.Id)).ToList();
				referencedProjects.Add(activeProject);
				var docs = referencedProjects?.SelectMany(x => x.Documents.Where(y => y.FilePath == filePath)).ToList();
				var doc = docs.FirstOrDefault();
				//This doc is not part of the current running solution, lets not send it over
				if (doc == null)
					return null;
				var newFiles = new List<(string FileName, string Code)>
				{
					(filePath, fileContents)
				}; ;

				var model = await doc.GetSemanticModelAsync();
				var compilation = model.Compilation;
				foreach (var c in partialClasses)
				{
					var symbols = compilation.GetSymbolsWithName(c.ClassName).ToList();// c.NameSpace == null ? c.ClassName : $"{c.NameSpace}.{c.ClassName}").ToList();
					var symbol = symbols.FirstOrDefault();
					var allFiles = symbol?.DeclaringSyntaxReferences.Select(x => x.SyntaxTree.FilePath).ToList();
					var files = allFiles.Where(x => x != filePath);
					await files?.ForEachAsync(1, (file) => Task.Run(() =>
						{
							var contents = System.IO.File.ReadAllText(file);
							newFiles.Add((file, contents));
						}));
				}
				return new EvalRequestMessage
				{
					Classes = classes,
					Files = newFiles,
				};

			}
			catch(Exception ex)
			{
				Console.WriteLine(ex);
			}
			return null;

		}

	}
}
