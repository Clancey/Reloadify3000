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
		static Assembly HarmonyLib => typeof(HarmonyLib.Harmony).Assembly;

		//From Harmonylib 2.2 -> 2.3 Memory becomes internal, and changes names. This lets it work for either version
		static Type PatchTools => _patchTools ??= HarmonyLib.GetType("HarmonyLib.Memory") ?? HarmonyLib.GetType("HarmonyLib.PatchTools");

		static MethodBase _detourMethod;
		static MethodBase DetourMethodCall => _detourMethod ??= PatchTools.GetMethod("DetourMethod", ALL_BINDING_FLAGS);

		public static void DetourMethod(MethodBase method, MethodBase replacement) => DetourMethodCall.Invoke(null, new object[] { method, replacement });
		//TODO: Handle crashes
	}
}
