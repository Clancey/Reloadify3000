using Mono.Debugger.Soft;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.HotReload
{
	internal static class DebuggerExtensions
	{
		internal static SoftEvaluationContext GetEvaluationContext (this SoftDebuggerSession debugger)
		{
			var ctx = debugger.ActiveThread.Backtrace.GetSoftEvaluationContext (0, EvaluationOptions.DefaultOptions);
			if (ctx == null)
				throw new NullReferenceException ("Failed to obtain Mono Debugger Evaluation Context.");
			return ctx;
		}

		static object GetServerBacktrace (this Backtrace backtrace)
			=> backtrace
				.GetType ()
				.GetField ("serverBacktrace", BindingFlags.NonPublic | BindingFlags.Instance)
				.GetValue (backtrace);

		internal static SoftEvaluationContext GetSoftEvaluationContext (this Backtrace backtrace, int frameIndex, EvaluationOptions options)
		{
			var serverBacktrace = backtrace.GetServerBacktrace ();
			return (SoftEvaluationContext)serverBacktrace
				.GetType ()
				.GetMethod ("GetEvaluationContext",
					BindingFlags.NonPublic |
					BindingFlags.Instance |
					BindingFlags.InvokeMethod)
				.Invoke (serverBacktrace, new object[] { frameIndex, options });
		}

		internal static MethodMirror GetMethod (this TypeMirror type, string name, params string[] parameterTypeNames)
		{
			var bindingFlags =
				BindingFlags.Static |
				BindingFlags.Public |
				BindingFlags.NonPublic |
				BindingFlags.Instance |
				BindingFlags.InvokeMethod;

			foreach (var method in type.GetMethodsByNameFlags (name, bindingFlags, true)) {
				var parameters = method.GetParameters ();
				if (method.Name == name && parameters.Length == (parameterTypeNames?.Length ?? 0)) {
					var paramTypesDoNotMatch = false;
					for (var i = 0; i < parameters.Length; i++) {
						var p = parameters[i];
						var expectedTypeName = parameterTypeNames[i];

						if (!p.ParameterType.FullName.Equals (expectedTypeName)) {
							paramTypesDoNotMatch = true;
							break;
						}
					}

					if (paramTypesDoNotMatch)
						continue;

					return method;
				}
			}

			return null;
		}

	}
}
