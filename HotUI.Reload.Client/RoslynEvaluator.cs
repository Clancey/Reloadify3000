#if NETSTANDARD2_0
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace HotUI.Internal.Reload {
	public class Evaluator : IEvaluator {

		public static bool IsSupported { get; }
		static Evaluator ()
		{
			try {
				CSharpScript.RunAsync ("2+2").Wait ();
				IsSupported = true;
			} catch (Exception ex) {
				IsSupported = false;
			}
		}
		public bool IsEvaluationSupported => IsSupported;

		ScriptOptions options;
		public async Task<bool> EvaluateCode (EvalRequestMessage request, EvalResult result)
		{
			if (string.IsNullOrEmpty (request.Code)) {
				return false;
			}

			EnsureConfigured ();
			try {
				ScriptState state;
				state = await CSharpScript.RunAsync (request.Code);

				result.Result = state.ReturnValue;
			} catch (CompilationErrorException ex) {
				//Log.Error ($"Error evaluating code");
				result.Messages = new EvalMessage [] { new EvalMessage ("error", ex.ToString ()) };
				return false;
			}
			return true;
		}

		void EnsureConfigured ()
		{
			if (options == null) {
				ConfigureVM ();
			}

		}

		void ConfigureVM ()
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies ().Where (a => !a.IsDynamic).ToArray ();
			options = ScriptOptions.Default.WithReferences (assemblies);
		}
	}
}
#endif