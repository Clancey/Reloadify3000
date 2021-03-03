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
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Reloadify.Internal;

namespace Reloadify {
	public class RoslynCodeManager {
		public static RoslynCodeManager Shared { get; set; } = new RoslynCodeManager ();

		Dictionary<string, List<string>> referencesForProjects = new Dictionary<string, List<string>> ();
		public async Task<bool> ShouldHotReload (Project project)
		{
			var shouldRun = (await SymbolFinder.FindDeclarationsAsync(project, "Reloadify", true)).Any();
			return shouldRun;
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
				var projects = solution.Projects.ToList();
				var activeProject = projects?.FirstOrDefault(x => x.FilePath == projectPath);
				var references = activeProject.ProjectReferences.Select(x=> x.ProjectId).ToList();
				var referencedProjects = projects.Where(x => references.Any(y => y == x.Id)).ToList();
				var docs = activeProject.Documents.Where(x => string.Equals(x.FilePath ,filePath, StringComparison.OrdinalIgnoreCase)).ToList();
				if(docs.Count == 0)
					docs = referencedProjects?.SelectMany(x => x.Documents.Where(y => string.Equals(y.FilePath, filePath, StringComparison.OrdinalIgnoreCase))).ToList();
				var doc = docs.FirstOrDefault();
				//This doc is not part of the current running solution, lets not send it over
				if (doc == null)
					return null;

				var assemblies = projects.Select(x => x.AssemblyName).Distinct();

				//We are going to build a file, with all the IgnoreAccessChecks so we don't get System.MethodAccessException when we call internal stuff
				var header = string.Join("\r\n", assemblies.Select(x => $"[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"{x}\")]"));
				var newFiles = new List<(string FileName, string Code)>
				{
					("IgnoreStuff",header),
					(filePath, fileContents)
				}; ;

				var model = await doc.GetSemanticModelAsync();
				var compilation = model.Compilation;
				var oldSyntaxTree = compilation.SyntaxTrees.FirstOrDefault(X => X.FilePath == filePath);

				var parseOptions = (CSharpParseOptions)oldSyntaxTree.Options;
				var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(fileContents, parseOptions,path:filePath);

				var root = syntaxTree.GetCompilationUnitRoot();
				var collector = new ClassCollector();
				collector.Visit(root);
				var classes = collector.Classes.Select(x => x.GetClassNameWithNamespace()).ToList();
				if (classes.Count == 0)
					return null;
				var partialClasses = collector.PartialClasses.Select(x => x.GetClassNameWithNamespace()).ToList();

				var newSyntaxTrees = new List<SyntaxTree>
				{
					syntaxTree
				};

				foreach (var c in partialClasses)
				{
					var symbols = compilation.GetSymbolsWithName(c.ClassName).ToList();// c.NameSpace == null ? c.ClassName : $"{c.NameSpace}.{c.ClassName}").ToList();
					
					var symbol = symbols.FirstOrDefault();
					var trees = symbol?.DeclaringSyntaxReferences.Where(x => !string.Equals(x.SyntaxTree.FilePath, filePath, StringComparison.OrdinalIgnoreCase)).ToList();
					
					await trees?.ForEachAsync(1, (tree) => Task.Run(() =>
						{
							newSyntaxTrees.Add(tree.SyntaxTree);
							var file = tree.SyntaxTree.FilePath;
							var contents = System.IO.File.ReadAllText(file);
							newFiles.Add((file, contents));
						}));
				}
				return new EvalRequestMessage
				{
					PreprocessorSymbolNames = parseOptions.PreprocessorSymbolNames.ToArray(),
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
