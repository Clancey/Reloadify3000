#if NETSTANDARD2_0
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;

namespace HotUI.Internal.Reload {
	public class Evaluator : IEvaluator {

		public static bool IsSupported { get; }
		static Evaluator ()
		{
			//try {
			//	CSharpScript.RunAsync ("2+2").Wait ();
			//	IsSupported = true;
			//} catch (Exception ex) {
			//	IsSupported = false;
			//}
			IsSupported = true;
		}
		public bool IsEvaluationSupported => IsSupported;

		ScriptOptions options;
		MetadataReference [] references;
		public async Task<bool> EvaluateCode (EvalRequestMessage request, EvalResult result)
		{
			if (string.IsNullOrEmpty (request.Code)) {
				return false;
			}

			EnsureConfigured ();
			try {


				foreach (var pair in request.Classes) {
					var shouldMixUp = this.IsExistingType (ToFullName (pair));
					if (shouldMixUp) {
						var c = pair.ClassName;
						var newC = pair.ClassName + Guid.NewGuid ().ToString ().Replace ("-", "");
						replacedClasses [c] = newC;
					} else
						replacedClasses [pair.ClassName] = pair.ClassName;
				}

				var newCode = Replace (request.Code, replacedClasses);

				var assembly = Compile (newCode);

				foreach (var c in request.Classes) {
					var code = $"{ToFullName (c.NameSpace, replacedClasses [c.ClassName])}";
					var  t = assembly.GetType (code);
					if(t != null) {
						if (t.IsSubclassOf (ViewType))
							result.FoundClasses.Add ((ToFullName (c), t));
						result.Result = t;
					}
				}

				return true;

				//result.Result = state.ReturnValue;
			} catch (Exception ex) {
				//Log.Error ($"Error evaluating code");
				result.Messages = new EvalMessage [] { new EvalMessage ("error", ex.ToString ()) };
				return false;
			}
		}


		Assembly Compile (string code )
		{
			var syntaxTree = CSharpSyntaxTree.ParseText (code);

			string assemblyName = System.IO.Path.GetRandomFileName ();

			CSharpCompilation compilation = CSharpCompilation.Create (
				assemblyName,
				syntaxTrees: new [] { syntaxTree },
				references: references,
				options: new CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary));

			using (var ms = new MemoryStream ()) {
				EmitResult emitResult = compilation.Emit (ms);

				if (!emitResult.Success) {
					IEnumerable<Diagnostic> failures = emitResult.Diagnostics.Where (diagnostic =>
						 diagnostic.IsWarningAsError ||
						 diagnostic.Severity == DiagnosticSeverity.Error);

					foreach (Diagnostic diagnostic in failures) {
						Console.Error.WriteLine ("{0}: {1}", diagnostic.Id, diagnostic.GetMessage ());
					}
				} else {
					ms.Seek (0, SeekOrigin.Begin);
					Assembly assembly = Assembly.Load (ms.ToArray ());
					return assembly;
				}
			}
			return null;
		}




		Assembly hotUIAssembly;
		Type _viewType;
		Type ViewType => _viewType ?? (_viewType = hotUIAssembly?.GetType ("HotUI.View"));
		Dictionary<string, string> replacedClasses = new Dictionary<string, string> ();



		static HashSet<string> newClasses = new HashSet<string> ();
		bool IsExistingType (string fullClassName)
		{
			if (newClasses.Contains (fullClassName))
				return false;
			try {

				var result = AppDomain.CurrentDomain.GetAssemblies ().FirstOrDefault (x => x.GetType (fullClassName) != null);
				if(result != null && result == hotUIAssembly) {
					newClasses.Add (fullClassName);
					return false;
				}
				return result != null;
			} catch (Exception ex) {
				newClasses.Add (fullClassName);
				return false;
			}
		}

		static string ToFullName ((string NameSpace, string ClassName) data) => ToFullName (data.NameSpace, data.ClassName);
		static string ToFullName (string NameSpace, string ClassName)
		{
			var name = string.IsNullOrWhiteSpace (NameSpace) ? "" : $"{NameSpace}.";
			return $"{name}{ClassName}";
		}

		static string Replace (string code, Dictionary<string, string> replaced)
		{

			string newCode = code;
			foreach (var pair in replaced) {
				if (pair.Key == pair.Value)
					continue;
				newCode = newCode.Replace ($" {pair.Key} ", $" {pair.Value} ");
				newCode = newCode.Replace ($" {pair.Key}(", $" {pair.Value}(");
				newCode = newCode.Replace ($" {pair.Key}:", $" {pair.Value}:");
			}
			return newCode;
		}

		void EnsureConfigured ()
		{
			if (options == null) {
				ConfigureVM ();
			}

		}

		void ConfigureVM ()
		{
			var refs = new List<MetadataReference>
				{
					//MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
					//MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
				};
			var assemblies = AppDomain.CurrentDomain.GetAssemblies ().Where (a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location)).ToArray ();
			var o = new CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary);
			foreach (var assembly in assemblies) {
				var name = assembly.GetName ().Name;
				if (name == "HotUI") {
					hotUIAssembly = assembly;
				}
				refs.Add (MetadataReference.CreateFromFile(assembly.Location));
			}
			//This should only happen in the tests
			if(hotUIAssembly == null) {
				refs.Add(MetadataReference.CreateFromFile ("HotUI.dll"));
				var filePath = System.IO.Path.Combine (Directory.GetCurrentDirectory (), "HotUI.dll");
				hotUIAssembly = Assembly.Load(filePath);
			}
			references = refs.ToArray ();
			options = ScriptOptions.Default.WithReferences (assemblies);
		}
	}
}
#endif