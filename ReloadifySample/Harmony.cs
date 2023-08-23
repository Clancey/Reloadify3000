using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReloadifySample
{
	public static class Harmony
	{
		static Type _patchTools;
		static Type PatchTools => _patchTools ??= typeof(HarmonyLib.HarmonyPatch).Assembly.GetType("HarmonyLib.PatchTools");

		static MethodBase _detourMethod;
		static MethodBase DetourMethodCall => _detourMethod ??= PatchTools.GetMethod("DetourMethod", HarmonyHotReloadHelper.ALL_BINDING_FLAGS);

		public static void DetourMethod(MethodBase method, MethodBase replacement)
		{
			DetourMethodCall.Invoke(null,new object[]{method, replacement});
		}
	}
}
