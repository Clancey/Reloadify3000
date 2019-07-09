using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HotUI.Internal.Reload;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace HotUI.Reload {
	public class IDEManager {

		public static IDEManager Shared { get; set; } = new IDEManager ();

		ITcpCommunicatorServer server;
		IDEManager ()
		{
			server = new TcpCommunicatorServer ();
			server.DataReceived = (o) => DataRecieved?.Invoke (o);
		}
		//TODO: change to fixed size dictionary
		Dictionary<string, string> currentFiles = new Dictionary<string, string> ();
		//Dictionary<string, string> currentReloadDlls = new Dictionary<string, string> ();
		Dictionary<string, List<string>> referencesForProjects = new Dictionary<string, List<string>> ();
		public async void HandleDocumentChanged (DocumentChangedEventArgs e)
		{
			try {
				if (server.ClientsCount == 0 && !Debugger.IsAttached)
					return;

				if (string.IsNullOrWhiteSpace (e.Filename))
					return;
				if (string.IsNullOrWhiteSpace (e.Text)) {
					var code = File.ReadAllText (e.Filename);
					if (string.IsNullOrWhiteSpace (code))
						return;
					e.Text = code;
				}
				if (currentFiles.TryGetValue (e.Filename, out var oldFile) && oldFile == e.Text) {
					return;
				}
				currentFiles [e.Filename] = e.Text;




				var tree = CSharpSyntaxTree.ParseText (e.Text);

				var references = GetReferences (e.ProjectFilePath, e.CurrentAssembly);

				var assembly = Compile (tree, references, e.Text);



				var root = tree.GetCompilationUnitRoot ();
				var collector = new ClassCollector ();
				collector.Visit (root);

				var classes = collector.Classes.Select (x => x.GetClassNameWithNamespace ()).ToList ();
				await server.Send (new EvalRequestMessage {
					Classes = classes,
					NewAssembly = File.ReadAllBytes(assembly),
					//Code = e.Text,
					FileName = e.Filename
				});
			} catch (Exception ex) {
				Console.WriteLine (ex);
			}
		}

		string Compile (SyntaxTree syntaxTree, List<string> references, string code)
		{
			string assemblyName = System.IO.Path.GetRandomFileName ();
			var metaReferences = references.Select (x => MetadataReference.CreateFromFile (x)).ToList ();

			CSharpCompilation compilation = CSharpCompilation.Create (
				assemblyName,
				references: metaReferences,
				syntaxTrees: new [] { syntaxTree },
				options: new CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary));

			var tempPath = System.IO.Path.GetTempFileName ();
			var emitResult = compilation.Emit (tempPath);

			if (!emitResult.Success) {
				IEnumerable<Diagnostic> failures = emitResult.Diagnostics.Where (diagnostic =>
					 diagnostic.IsWarningAsError ||
					 diagnostic.Severity == DiagnosticSeverity.Error);

				foreach (Diagnostic diagnostic in failures) {
					Console.Error.WriteLine ("{0}: {1}", diagnostic.Id, diagnostic.GetMessage ());
				}
			} else {
				//Assembly assembly = Assembly.LoadFile (tempPath);
				//return assembly;
				return tempPath;
			}
			//}
			return null;
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

		public Action<object> DataRecieved { get; set; }

		public void StartMonitoring ()
		{
			StartMonitoring (Constants.DEFAULT_PORT);
		}

		internal void StartMonitoring (int port)
		{
			server.StartListening (port);
		}

		public void StartDebugging ()
		{
			StartMonitoring (Constants.DEFAULT_PORT);
			currentFiles.Clear ();
			referencesForProjects.Clear ();
		}
		public void StopDebugging ()
		{
			server.StopListening ();
		}
	}
}
