using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("HarmonyLib")]
namespace ReloadifySample
{
	public class HarmonyHotReloadHelper
	{

 		public const BindingFlags ALL_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.Static | BindingFlags.Instance |
                                               BindingFlags.FlattenHierarchy;
            
        const BindingFlags ALL_DECLARED_METHODS_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
                                                                BindingFlags.Static | BindingFlags.Instance |
                                                                BindingFlags.DeclaredOnly; //only declared methods can be redirected, otherwise it'll result in hang


		static readonly List<Type> ExcludeMethodsDefinedOnTypes = new List<Type>
        {
            //typeof(MonoBehaviour),
            //typeof(Behaviour),
            //typeof(UnityEngine.Object),
           	//typeof(Component),
            typeof(System.Object)
        }; 

		public static async void Init(){
			Console.WriteLine("Harmony!");
			Reloadify.Reload.Instance.ReplaceType = (d) => ReplaceType(d.ClassName, d.Type);
		} 
		public static void ReplaceType(string className, Type newType)
		{

			try{
				Console.WriteLine($"HotReloaded!: {className} - {newType}");
				var replaceType = newType.GetMethod("ReplaceType", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
				if (replaceType != null && newType != MethodBase.GetCurrentMethod().DeclaringType) 
				{ 
					Console.WriteLine($"Calling Replacetype and moving on :{newType}");
					replaceType?.Invoke(null,new object[]{className,newType});
					Console.WriteLine("ReplaceType completed");
					return;
				}

				foreach (var prop in newType.GetProperties())
				{
					Console.WriteLine($"\t{prop.Name}");
				} 
				var oldClass = Type.GetType(className);
				var oldMethods = oldClass.GetMethods(ALL_DECLARED_METHODS_BINDING_FLAGS);
				var newMethods = newType.GetMethods(ALL_DECLARED_METHODS_BINDING_FLAGS);
				var allDeclaredMethodsInExistingType = oldClass.GetMethods(ALL_DECLARED_METHODS_BINDING_FLAGS)
								.Where(m => !ExcludeMethodsDefinedOnTypes.Contains(m.DeclaringType))
								.ToList();
				foreach(var method in newMethods)
				{
					Console.WriteLine($"Method: {method.Name}");
					var oldMethod = oldMethods.FirstOrDefault(m => m.Name == method.Name);
					if(oldMethod != null && !method.IsGenericMethod)
					{
						//Found the method. Lets Monkey patch it
						Harmony.DetourMethod(oldMethod, method);
					}
					else{
					//Lets not worry about it for now
					}

				}
				//Call static init if it exists on new classes!
				var staticInit = newType.GetMethod("Init", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
				if (staticInit != null)
				{
					Console.WriteLine($"Calling static Init on :{newType}");
					staticInit?.Invoke(null, null);
					Console.WriteLine("Init completed");
				}

			}
			catch(Exception ex){
				Console.WriteLine(ex);
			}
		}
	}
}
