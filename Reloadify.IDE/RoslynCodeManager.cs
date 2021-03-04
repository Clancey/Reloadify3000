﻿using System;
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
using Microsoft.CodeAnalysis.Emit;
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
		public async Task<bool> ShouldStartDebugging ()
		{
			currentCompilationCount = 0;
			var projects = IDEManager.Shared.Solution.Projects.ToList();
			CurrentActiveProject = projects?.FirstOrDefault(x => x.FilePath == IDEManager.Shared.CurrentProjectPath);
			return await ShouldHotReload(CurrentActiveProject);

		}
		Project CurrentActiveProject;
		public void StopDebugging ()
		{
			referencesForProjects.Clear ();
			currentTrees.Clear();
			CleanupFiles();
		}

		void CleanupFiles()
		{
			try
			{ 
				var outputDirectory = Path.GetDirectoryName(CurrentActiveProject.OutputFilePath);
				var oldFiles = Directory.GetFiles(outputDirectory, $"{tempDllStart}*").ToList();
				foreach (var f in oldFiles)
					File.Delete(f);
			}
			catch
			{

			}
		}
		const string tempDllStart = "Reloadify-emit-";
		static int currentCompilationCount = 0;
		Dictionary<string, SyntaxTree> currentTrees = new Dictionary<string, SyntaxTree>();
		public async System.Threading.Tasks.Task<EvalRequestMessage> SearchForPartialClasses(string filePath, string fileContents,string projectPath, Microsoft.CodeAnalysis.Solution solution)
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
				var newFiles = new List<string>
				{
					filePath
				};
				var model = await doc.GetSemanticModelAsync();
				
				var compilation = model.Compilation;
				var oldSyntaxTree = compilation.SyntaxTrees.FirstOrDefault(X => X.FilePath == filePath);
				var parseOptions = (CSharpParseOptions)oldSyntaxTree.Options;
				var syntaxTree = CSharpSyntaxTree.ParseText(fileContents, parseOptions,path:filePath,encoding: System.Text.Encoding.Default);
				var ignoreSyntaxTree = CSharpSyntaxTree.ParseText(header, parseOptions);
				var root = syntaxTree.GetCompilationUnitRoot();
				var collector = new ClassCollector();
				collector.Visit(root);
				var classes = collector.Classes.Select(x => x.GetClassNameWithNamespace()).ToList();
				if (classes.Count == 0)
					return null;
				var partialClasses = collector.PartialClasses.Select(x => x.GetClassNameWithNamespace()).ToList();

				currentTrees[filePath] = syntaxTree;
				currentTrees["IgnoresAccessChecksTo"] = ignoreSyntaxTree;

				foreach (var c in partialClasses)
				{
					var symbols = compilation.GetSymbolsWithName(c.ClassName).ToList();// c.NameSpace == null ? c.ClassName : $"{c.NameSpace}.{c.ClassName}").ToList();
					
					var symbol = symbols.FirstOrDefault();
					var trees = symbol?.DeclaringSyntaxReferences.Where(x => !string.Equals(x.SyntaxTree.FilePath, filePath, StringComparison.OrdinalIgnoreCase)).ToList();
					
					await trees?.ForEachAsync(1, (tree) => Task.Run(() =>
						{
							currentTrees[tree.SyntaxTree.FilePath] = tree.SyntaxTree;
							var file = tree.SyntaxTree.FilePath;
							var contents = System.IO.File.ReadAllText(file);
							newFiles.Add(file);
						}));
				}


				//Lets compile
				var dllMS = new MemoryStream();
				var pdbMS = new MemoryStream();
				var newAssemblyName = $"{tempDllStart}{currentCompilationCount++}";
				var outputDirectory = Path.GetDirectoryName(activeProject.OutputFilePath);

				var activeCompilation = await activeProject.GetCompilationAsync();
				var compileReferences = compilation.References.ToList();
				compileReferences.AddRange(activeCompilation.References);
				compileReferences.Add(MetadataReference.CreateFromFile(activeProject.OutputFilePath));

				//This allows you to compile using private references
				var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithMetadataImportOptions(MetadataImportOptions.All);
				var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic);
				topLevelBinderFlagsProperty.SetValue(compilationOptions, (uint)1 << 22);

				var newCompilation = CSharpCompilation.Create(newAssemblyName, syntaxTrees: currentTrees.Values, references: compileReferences, options: compilationOptions);
				var dllPath = Path.Combine(outputDirectory, $"{newAssemblyName}.dll");
				var pdbPath = Path.Combine(outputDirectory, $"{newAssemblyName}.pdb");

				var result = newCompilation.Emit(dllMS, pdbMS, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
				if (!result.Success)
				{
					IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
						diagnostic.IsWarningAsError ||
						diagnostic.Severity == DiagnosticSeverity.Error).ToList();

					IDEManager.Shared.OnErrors?.Invoke(failures);
					foreach (Diagnostic diagnostic in failures)
					{
						Console.Error.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
					}
				}
				else
				{
					IDEManager.Shared.OnErrors?.Invoke(null);
					var resp  = new EvalRequestMessage
					{
						AssemblyName = newAssemblyName,
						Assembly = dllMS.GetBuffer(),
						Pdb = pdbMS.GetBuffer(),
						Classes = classes,
					};

					File.WriteAllBytes(dllPath, resp.Assembly);
					File.WriteAllBytes(pdbPath, resp.Pdb);
					return resp;
				}
			}
			catch(Exception ex)
			{
				Console.WriteLine(ex);
			}
			return null;
		}

	}
}
