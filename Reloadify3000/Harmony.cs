using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Reloadify
{
	public static class Harmony
	{
		public const BindingFlags ALL_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
											  BindingFlags.Static | BindingFlags.Instance |
											  BindingFlags.FlattenHierarchy;
		static Type _patchTools;
		static Type PatchTools => _patchTools ??= typeof(HarmonyLib.HarmonyPatch).Assembly.GetType("HarmonyLib.PatchTools");

		static MethodBase _detourMethod;
		static MethodBase DetourMethodCall => _detourMethod ??= PatchTools.GetMethod("DetourMethod", ALL_BINDING_FLAGS);

		public static void DetourMethod(MethodBase method, MethodBase replacement)
		{
			DetourMethodCall.Invoke(null, new object[] { method, replacement });
		}
	}
}
