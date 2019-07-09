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

namespace Xamarin.HotReload.Ide
{
    public class RoslynCodeManager
    {
        public static RoslynCodeManager Shared { get; set; } = new RoslynCodeManager();

        Dictionary<string, string> currentFiles = new Dictionary<string, string>();
        Dictionary<string, List<string>> referencesForProjects = new Dictionary<string, List<string>>();
        Dictionary<string, Assembly> currentAssemblies = new Dictionary<string, Assembly>();
        Dictionary<string, string> replacedClasses = new Dictionary<string, string>();

        public bool ShouldHotReload(string project)
        {
            if (string.IsNullOrWhiteSpace(project))
                return false;
            var hasHotReload = GetReferences(project, null).Any(x=> x.EndsWith("HotUI.dll"));
            return hasHotReload;
        }
        public void StartDebugging()
        {
        }
        public void StopDebugging()
        {
            currentFiles.Clear();
            referencesForProjects.Clear();
            currentAssemblies.Clear();
            newClasses.Clear();
            replacedClasses.Clear();
        }

        public void PrimeProject(string project, string dll)
        {
            var assembly = Assembly.LoadFile(dll);
            currentAssemblies[dll] = assembly;
            Task.Run(() =>
            {
                GetReferences(project, dll);
            });
        }

        public void HandleFileChange(FileIdentity file)
        {
            if (string.IsNullOrWhiteSpace(file.SourcePath))
                return;
            var code = File.ReadAllText(file.SourcePath);
            if (string.IsNullOrWhiteSpace(code))
                return;

            if (currentFiles.TryGetValue(file.SourcePath, out var oldFile) && oldFile == code)
            {
                return;
            }
            currentFiles[file.SourcePath] = code;

            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetCompilationUnitRoot();
            var collector = new ClassCollector();
            collector.Visit(root);

            //var classes = collector.Classes.Select(x => x.GetClassNameWithNamespace()).ToList();

            Dictionary<string, string> renamedClasses = new Dictionary<string, string>();
            bool didChangeName = false;
            foreach (var theClass in collector.Classes)
            {
                var pair = theClass.GetClassNameWithNamespace();
                var fullName = ToFullName(pair);
                var shouldMixUp = IsExistingType(fullName, file.CurrentAssemblyLocation);
                if (shouldMixUp)
                {
                    didChangeName = true;
                    var c = pair.ClassName;
                    var newC = pair.ClassName + Guid.NewGuid().ToString().Replace("-", "");
                    replacedClasses[c] = newC;
                    renamedClasses[fullName] = ToFullName(pair.NameSpace, newC);
                    //var newTheClass = theClass.WithIdentifier( newC);
                    //root.ReplaceNode(theClass, newTheClass);
                }
                else
                {
                    replacedClasses[pair.ClassName] = pair.ClassName;
                    renamedClasses[fullName] = fullName;
                }
            }

            code = Replace(code, replacedClasses);
            file.Classes = renamedClasses;

            if(didChangeName)
                tree = CSharpSyntaxTree.ParseText(code);

            var references = GetReferences(file.RelativePath, file.CurrentAssemblyLocation);

            var assembly = Compile(tree, references, code);
            file.NewAssembly = assembly;


            //await server.Send (new EvalRequestMessage {
            //	Classes = classes,
            //	NewAssembly = File.ReadAllBytes (assembly),
            //	//Code = e.Text,
            //	FileName = e.Filename
            //});

        }

        string Compile(SyntaxTree syntaxTree, List<string> references, string code)
        {
            string assemblyName = System.IO.Path.GetRandomFileName();
            var metaReferences = references.Select(x => MetadataReference.CreateFromFile(x)).ToList();

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                references: metaReferences,
                syntaxTrees: new[] { syntaxTree },
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var tempPath = System.IO.Path.GetTempFileName();
            var emitResult = compilation.Emit(tempPath);

            if (!emitResult.Success)
            {
                IEnumerable<Diagnostic> failures = emitResult.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);

                foreach (Diagnostic diagnostic in failures)
                {
                    Console.Error.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                }
            }
            else
            {
                //Assembly assembly = Assembly.LoadFile (tempPath);
                //return assembly;
                return tempPath;
            }
            //}
            return null;
        }

        static HashSet<string> newClasses = new HashSet<string>();
        bool IsExistingType(string fullClassName, string dll)
        {
            if (newClasses.Contains(fullClassName))
                return false;
            try
            {
                if (!currentAssemblies.TryGetValue(dll, out var assembly))
                    currentAssemblies[dll] = assembly = Assembly.LoadFile(dll);

                var exists = TypeExists(assembly,fullClassName);
                
                if (!exists && assembly.FullName != "HotUI")
                {
                    newClasses.Add(fullClassName);
                    return false;
                }
                return exists;
            }
            catch (Exception ex)
            {
                newClasses.Add(fullClassName);
                return false;
            }
        }
        static bool TypeExists(Assembly assembly, string fullName)
        {
            try
            {
                var types = assembly.GetTypes();
                return assembly.GetType(fullName) != null;
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Any(t => t?.DeclaringType?.FullName == fullName);
            }
        }
        static string ToFullName((string NameSpace, string ClassName) data) => ToFullName(data.NameSpace, data.ClassName);
        static string ToFullName(string NameSpace, string ClassName)
        {
            var name = string.IsNullOrWhiteSpace(NameSpace) ? "" : $"{NameSpace}.";
            return $"{name}{ClassName}";
        }

        static string Replace(string code, Dictionary<string, string> replaced)
        {

            string newCode = code;
            foreach (var pair in replaced)
            {
                if (pair.Key == pair.Value)
                    continue;
                newCode = newCode.Replace($" {pair.Key} ", $" {pair.Value} ");
                newCode = newCode.Replace($" {pair.Key}(", $" {pair.Value}(");
                newCode = newCode.Replace($" {pair.Key}:", $" {pair.Value}:");
            }
            return newCode;
        }


        public List<string> GetReferences(string projectPath, string currentReference)
        {
            if (referencesForProjects.TryGetValue(projectPath, out var references))
                return references;
            var project = new ProjectInstance(projectPath);
            var result = BuildManager.DefaultBuildManager.Build(
                new BuildParameters(),
                new BuildRequestData(project, new[]
            {
                "ResolveProjectReferences",
                "ResolveAssemblyReferences"
            }));

            IEnumerable<string> GetResultItems(string targetName)
            {
                var buildResult = result.ResultsByTarget[targetName];
                var buildResultItems = buildResult.Items;

                return buildResultItems.Select(item => item.ItemSpec);
            }

            references = GetResultItems("ResolveProjectReferences")
                .Concat(GetResultItems("ResolveAssemblyReferences")).Distinct().ToList();
            if (!string.IsNullOrWhiteSpace(currentReference))
                references.Add(currentReference);
            referencesForProjects[projectPath] = references;
            return references;
        }


        public class ClassCollector : CSharpSyntaxWalker
        {
            public ICollection<UsingDirectiveSyntax> Usings { get; } = new List<UsingDirectiveSyntax>();
            public ICollection<ClassDeclarationSyntax> Classes { get; } = new List<ClassDeclarationSyntax>();

            public override void VisitUsingDirective(UsingDirectiveSyntax node)
            {
                base.VisitUsingDirective(node);
                Usings.Add(node);
            }
            public override void VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                base.VisitClassDeclaration(node);
                if (!(node.Parent is ClassDeclarationSyntax))
                    Classes.Add(node);
            }

        }
    }

    public static class SyntaxNodeHelper
    {
        public static bool TryGetParentSyntax<T>(SyntaxNode syntaxNode, out T result)
        where T : SyntaxNode
        {
            // set defaults
            result = null;

            if (syntaxNode == null)
            {
                return false;
            }

            try
            {
                syntaxNode = syntaxNode.Parent;

                if (syntaxNode == null)
                {
                    return false;
                }

                if (syntaxNode.GetType() == typeof(T))
                {
                    result = syntaxNode as T;
                    return true;
                }

                return TryGetParentSyntax<T>(syntaxNode, out result);
            }
            catch
            {
                return false;
            }
        }

        public static (string NameSpace, string ClassName) GetClassNameWithNamespace(this ClassDeclarationSyntax c)
        {
            NamespaceDeclarationSyntax namespaceDeclaration;
            TryGetParentSyntax(c, out namespaceDeclaration);
            var theNameSpace = namespaceDeclaration?.Name?.ToString() ?? "";
            return (theNameSpace, c.Identifier.ToString());
        }
    }

}
