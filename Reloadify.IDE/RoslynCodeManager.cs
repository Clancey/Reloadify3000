using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
			if (project.Name == "Reloadify.VS" || project.Name == "Reloadify.VSMac" || project.Name == "Reloadify.CommandLine")
				return false;
			var shouldRun = (await SymbolFinder.FindDeclarationsAsync(project, "Reloadify", true)).Any();
			if(!shouldRun)
				shouldRun = (await SymbolFinder.FindDeclarationsAsync(project, "Comet.Reload", true)).Any();
			return shouldRun;
		}
		bool isUnity = false;
		public void StartDebugging ()
		{
			var projects = IDEManager.Shared.Solution.Projects.ToList();
			CurrentActiveProject = GetActiveProject(projects);
			currentCompilationCount = 0;
			referencesForProjects.Clear();
			currentTrees.Clear();
			CleanupFiles();

		}
		public string ProjectFlavor;

		Project GetActiveProject(List<Project> projects)
		{
			Project project = null;
			if(!string.IsNullOrWhiteSpace(ProjectFlavor))
				project = projects?.FirstOrDefault(x => x.FilePath.EndsWith(IDEManager.Shared.CurrentProjectPath) && x.Name.Contains(ProjectFlavor));
			return project ?? projects?.FirstOrDefault(x => x.FilePath.EndsWith(IDEManager.Shared.CurrentProjectPath));
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
				if (string.IsNullOrWhiteSpace(CurrentActiveProject?.OutputFilePath))
					return;
				var outputDirectory = Path.GetDirectoryName(CurrentActiveProject.OutputFilePath);
				var oldFiles = Directory.GetFiles(outputDirectory, $"{tempDllName}*").ToList();
				foreach (var f in oldFiles)
					File.Delete(f);
			}
			catch
			{

			}
		}
		const string tempDllName = "Reloadify-emit";
		static int currentCompilationCount = 0;
		ConcurrentDictionary<string, SyntaxTree> currentTrees = new ConcurrentDictionary<string, SyntaxTree>();

		public void Rename(string oldPath, string newPath)
		{
			if(currentTrees.TryGetValue(oldPath, out var tree))
			{
				currentTrees[newPath] = tree;
				currentTrees.TryRemove(oldPath, out var f);
			}
		}
		public void Delete(string path)
		{
			if (currentTrees.ContainsKey(path))
				currentTrees.TryRemove(path, out var f);
		}
		public HashSet<string> NewFiles = new ();
		LanguageVersion currentLanguageVersion = LanguageVersion.Default;
		public async System.Threading.Tasks.Task<EvalRequestMessage> SearchForPartialClasses(string filePath, string fileContents,string projectPath, Microsoft.CodeAnalysis.Solution solution)
		{
			try
			{
				var projects = solution.Projects.ToList();
				var activeProject = GetActiveProject(projects);
				if (activeProject == null){
					Console.WriteLine("Error: There is no active Projects");
					return null;
				}
				var references = activeProject.ProjectReferences?.Select(x=> x.ProjectId).ToList();
				var referencedProjects = projects.Where(x => references?.Any(y => y == x.Id) ?? false).ToList();
				var docs = activeProject.Documents.Where(x => x?.FilePath?.EndsWith(filePath, StringComparison.OrdinalIgnoreCase) ?? false).ToList();
				if(docs.Count == 0)
					docs = referencedProjects?.SelectMany(x => x.Documents.Where(y => y?.FilePath?.EndsWith(filePath, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
				var doc = docs.FirstOrDefault();
				//"/Users/clancey/Projects/FashionPactPos/App/FashionPact/Views/Test2.cs"
				//"/Users/clancey/Projects/FashionPactPos/App/FashionPact/Views/Test2.cs"
				//This doc is not part of the current running solution, lets not send it over
				if (doc == null && !NewFiles.Any(x=> x.EndsWith(filePath, StringComparison.OrdinalIgnoreCase))){
					Console.WriteLine("The doc was not found in part of a project. Ignoring");
					return null;
				}
				
				//On windows sometimes its a file path...
				var assemblies = projects.Where(x=> !x.AssemblyName.Contains(x.FilePath)).Select(x => x.AssemblyName).Distinct();
				
				//We are going to build a file, with all the IgnoreAccessChecks so we don't get System.MethodAccessException when we call internal stuff
				var header = string.Join("\r\n", assemblies.Select(x => $"[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"{x}\")]"));
				var newFiles = new List<string>
				{
					filePath
				};
				var model = await (doc?.GetSemanticModelAsync() ?? Task.FromResult<SemanticModel>(null));

				var compilation = model?.Compilation;
				var oldSyntaxTree = compilation?.SyntaxTrees.FirstOrDefault(X => X.FilePath.EndsWith(filePath, StringComparison.OrdinalIgnoreCase));
				var parseOptions = (CSharpParseOptions)(oldSyntaxTree?.Options ?? activeProject.ParseOptions);
				//parseOptions.WithLanguageVersion(LanguageVersion.Preview)
				//Always use the highest version
				currentLanguageVersion = (LanguageVersion)Math.Max((int)currentLanguageVersion, (int)parseOptions.LanguageVersion);
				bool versionChanged = false;
				if(parseOptions.LanguageVersion != currentLanguageVersion)
				{
					versionChanged = true;
					parseOptions = parseOptions.WithLanguageVersion(currentLanguageVersion);
				}
				var syntaxTree = CSharpSyntaxTree.ParseText(fileContents, parseOptions,path:filePath,encoding: System.Text.Encoding.Default);
				var ignoreSyntaxTree = CSharpSyntaxTree.ParseText(header, parseOptions);
				var root = syntaxTree.GetCompilationUnitRoot();
				var collector = new ClassCollector();
				collector.Visit(root);
				var classes = collector.Classes.Select(x => x.GetClassNameWithNamespace()).ToList();
				if (classes.Count == 0){
					Console.WriteLine("No classes found in the file");
					return null;
				}
				var partialClasses = collector.PartialClasses.Select(x => x.GetClassNameWithNamespace()).ToList();

				currentTrees[filePath] = syntaxTree;
				currentTrees["IgnoresAccessChecksTo"] = ignoreSyntaxTree;

				var assemblyVersion = currentCompilationCount++;
				currentTrees["Relodify-Emit-AssemblyVersion"] = CSharpSyntaxTree.ParseText($"[assembly: System.Reflection.AssemblyVersionAttribute(\"1.0.{assemblyVersion}\")]", parseOptions);
				foreach (var c in partialClasses)
				{
					var symbols = compilation?.GetSymbolsWithName(c.ClassName).ToList();// c.NameSpace == null ? c.ClassName : $"{c.NameSpace}.{c.ClassName}").ToList();
					
					var symbol = symbols?.FirstOrDefault();
					var trees = symbol?.DeclaringSyntaxReferences.Where(x => !x.SyntaxTree.FilePath.EndsWith(filePath, StringComparison.OrdinalIgnoreCase)).ToList();
					
					await trees?.ForEachAsync(1, (tree) => Task.Run(() =>
					{
						var fileTree = tree.SyntaxTree;
						var file = fileTree.FilePath;
						currentTrees[file] = fileTree;
						newFiles.Add(file);
					}));
				}


				//Lets compile
				var dllMS = new MemoryStream();
				var pdbMS = new MemoryStream();
				var newAssemblyName = $"{tempDllName}-{assemblyVersion}";
				var outputDirectory = Path.GetDirectoryName(activeProject.OutputFilePath);

				var activeCompilation = await activeProject.GetCompilationAsync();

				var usings = activeCompilation.SyntaxTrees.SelectMany(x=> x.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>().Where(x=> x.GlobalKeyword.ValueText == "global")).Select(x=> x.ToString()).Distinct().ToList();


				currentTrees["GlobalUsings"] = CSharpSyntaxTree.ParseText( string.Join(" ",usings), parseOptions);
				var compilationDictionary = new Dictionary<string, MetadataReference>();
				if(compilation != null)
				foreach (var r in compilation.References)
					compilationDictionary[Path.GetFileName(r.Display)] = r;

				foreach(var r in activeCompilation.References)
				{
					compilationDictionary[Path.GetFileName(r.Display)] = r;
				}
				var activeReferences = activeCompilation.References.OrderBy(x => x.Display).ToList();
				var compileReferences = compilationDictionary.Values.ToList();
				var isUnity = activeProject.AnalyzerReferences.Any(x => x.Display == "Microsoft.Unity.Analyzers");
				compileReferences.Add(CreateMetaReference(activeProject.OutputFilePath,isUnity));

				//This allows you to compile using private references
				var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithMetadataImportOptions(MetadataImportOptions.All);
				var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic);
				topLevelBinderFlagsProperty.SetValue(compilationOptions, (uint)1 << 22);

				//Version check the trees, if the version changed!
				if(versionChanged)
				{
					foreach(var pair in currentTrees.ToList())
					{
						var tree = pair.Value;
						var options = (CSharpParseOptions)tree.Options;
						if (options.LanguageVersion != currentLanguageVersion)
						{
							var fileTree = pair.Value;
							var file = fileTree.FilePath;
							var text = fileTree.GetText().ToString();
							fileTree = CSharpSyntaxTree.ParseText(text, options, path: file, encoding: System.Text.Encoding.Default);
							currentTrees[file] = fileTree;
						}
					}
				}

				var newCompilation = CSharpCompilation.Create($"{tempDllName}-{assemblyVersion}", syntaxTrees: currentTrees.Values, references: compileReferences, options: compilationOptions);
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
						var span = diagnostic.Location.GetLineSpan();
						IDEManager.Shared.Log?.Invoke($"{diagnostic.Severity}: {diagnostic.Location.SourceTree.FilePath}");
						IDEManager.Shared.Log?.Invoke($"\t Line: {span.StartLinePosition.Line} - {span.StartLinePosition.Character}");
						IDEManager.Shared.Log?.Invoke($"\t{diagnostic.Id}: {diagnostic.GetMessage()}");
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
				IDEManager.Shared.Log?.Invoke(ex.Message);
				Console.WriteLine(ex);
			}
			return null;
		}

		static MetadataReference CreateMetaReference(string filePath, bool isUnity)
        {
			var foundFilePath = filePath;
			if (!File.Exists(filePath) && !isUnity)
			{
				var filename = Path.GetFileName(filePath);
				var libFilePath = filePath.Substring(0,filePath.IndexOf("Temp"));
				libFilePath = Path.Combine(libFilePath,"Library", "ScriptAssemblies", filename);
				foundFilePath = libFilePath;
			}
			return MetadataReference.CreateFromFile(foundFilePath);
        }

    }
}
