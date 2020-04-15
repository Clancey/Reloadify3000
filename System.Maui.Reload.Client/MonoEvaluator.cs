#if !NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Mono.CSharp;


namespace System.Maui.Internal.Reload {
	public partial class Evaluator : IEvaluator {
		static Evaluator ()
		{
			Debug.WriteLine("Using Mono.CSharp.Evaluator");
			var eval = new Mono.CSharp.Evaluator (new CompilerContext (new CompilerSettings (), new Printer ()));
			try {
				eval.Evaluate ("2+2");
				IsSupported = true;
			} catch (Exception ex) {
				//Log.Error ("Runtime evaluation not supported, did you set the mtouch option --enable-repl?");
				IsSupported = false;
			}
		}
		public static bool IsSupported { get; }
		public bool IsEvaluationSupported => IsSupported;
		partial void PlatformSettings (CompilerSettings settings);
		partial void PlatformInit ();

		Mono.CSharp.Evaluator eval;
		Printer printer;
		public Task<bool> EvaluateCode (EvalRequestMessage request, EvalResult result)
		{
			if (string.IsNullOrEmpty (request.Code) || request.Classes?.Count <= 0) {
				return Task.FromResult (false);
			}

			EnsureConfigured ();
			return Evaluate (request, result, true);
		}
		
		async Task<bool> Evaluate (EvalRequestMessage request, EvalResult result, bool retryOnError)
		{
			try {
				printer.Reset ();
				object retResult;
				bool result_set;


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
				Debug.WriteLine(newCode);
				var foo = eval.Compile (newCode);
				foreach (var c in request.Classes) {
					var code = $"typeof({ToFullName(c.NameSpace,replacedClasses[c.ClassName])})";
					Debug.WriteLine(code);
					var s = eval.Evaluate (code, out retResult, out result_set);
					if (result_set) {
						var t = (Type)retResult;
						//if (t.IsSubclassOf (ViewType) || HandlerType.IsAssignableFrom(t)) {
							result.FoundClasses.Add ((ToFullName(c), t));
						//}

						result.Result = retResult;
					}
				}
				return true;
			} catch (Exception ex) {
				if (retryOnError) {
					eval = null;
					EnsureConfigured ();
					return await Evaluate (request, result, false);
				}

				//Log.Error ($"Error evalutaing code");
				eval = null;
				if (printer.Messages.Count != 0) {
					result.Messages = printer.Messages.ToArray ();
				} else {
					result.Messages = new [] { new EvalMessage ("error", ex.ToString ()) };
				}
				return false;
			}
		}

		static HashSet<string> newClasses = new HashSet<string> ();
		bool IsExistingType(string fullClassName)
		{
			if (newClasses.Contains (fullClassName))
				return false;
			try {
				var code = $"typeof({fullClassName})";
				var s = eval.Evaluate (code, out var retResult, out var result_set);
				if(result_set && ((Type)retResult).Assembly == CometAssembly) {
					newClasses.Add (fullClassName);
					return false;
				}

				return result_set;
			}
			catch(Exception ex) {
				newClasses.Add (fullClassName);
				return false;
			}
		}

		

		void EnsureConfigured ()
		{
			if (eval != null) {
				return;
			}

			var settings = new CompilerSettings ();
			settings.AddConditionalSymbol ("DEBUG");
			PlatformSettings (settings);
			printer = new Printer ();
			var context = new CompilerContext (settings, printer);
			eval = new Mono.CSharp.Evaluator (context);
			AppDomain.CurrentDomain.AssemblyLoad += (_, e) => {
				LoadAssembly (e.LoadedAssembly);
			};
			AppDomain.CurrentDomain.GetAssemblies ()
				.Where (a => !a.IsDynamic).ToList()
				.ForEach(LoadAssembly);

			
			if (CometAssembly == null)
				eval.LoadAssembly ("System.Maui.dll");


			//
			// Add default namespaces
			//
			object res;
			bool hasRes;
			eval.Evaluate ("using System;", out res, out hasRes);
			eval.Evaluate ("using System.Collections.Generic;", out res, out hasRes);
			eval.Evaluate ("using System.Linq;", out res, out hasRes);
			PlatformInit ();
		}

		void LoadAssembly (Assembly assembly)
		{
			var name = assembly.GetName ().Name;
			if (name == "mscorlib" || name == "System" || name == "System.Core" || name.StartsWith ("eval-"))
				return;
			if(name == "System.Maui") {
				CometAssembly = assembly;
			}
			eval?.ReferenceAssembly (assembly);
		}
	}

	class Printer : ReportPrinter {
		public readonly List<EvalMessage> Messages = new List<EvalMessage> ();

		public new void Reset ()
		{
			Messages.Clear ();
			base.Reset ();
		}

		public override void Print (AbstractMessage msg, bool showFullPath)
		{
			if (msg.MessageType != "error") {
				return;
			}
			AddMessage (msg.MessageType, msg.Text, msg.Location.Row, msg.Location.Column);
		}

		public void AddError (Exception ex)
		{
			AddMessage ("error", ex.ToString (), 0, 0);
		}

		void AddMessage (string messageType, string text, int line, int column)
		{
			var m = new EvalMessage (messageType, text, line, column);
			Messages.Add (m);
			if (m.MessageType == "error") {
				//Log.Error (m.Text);
			}
		}
	}
}
#endif