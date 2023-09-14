using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Esp;
using Esp.Resources;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using Reloadify.Internal;

namespace Reloadify {
	/// <summary>
	/// Preview server that process HTTP requests, evaluates them in the <see cref="VM"/>
	/// and preview them with the <see cref="Previewer"/>.
	/// </summary>
	public class Reload {

		public Action StartingReload { get; set; }
		public Action<(string ClassName, Type Type)> ReplaceType { get; set; }
		public Action FinishedReload { get; set; }
		//TaskScheduler mainScheduler;
		bool isRunning;
		ICommunicatorClient client;
		public bool DisableHarmonyIntegration { get; set; }
		public static Reload Instance { get; } = new Reload ();

		internal Reload ()
		{

		}

		public static Task<bool> Init (string ideIP = null, int idePort = Constants.DEFAULT_PORT)
		{
			if (Instance.isRunning) {
				return Task.FromResult (true);
			}
			return Instance.RunInternal (ideIP, idePort);
		}

		internal async Task<bool> RunInternal (string ideIP, int idePort)
		{
			if (isRunning) {
				return true;
			}
			var tcpClients = TcpCommunicatorClient.GetTcpCommunicatorsFromResource ();
			if (ideIP != null)
				tcpClients.Insert (0, new TcpCommunicatorClient { Ip = ideIP, Port = idePort });
			this.client = await DiscoveryService.Shared.FindConnection (tcpClients.ToArray ());

			if (client == null)
				return false;

			client.DataReceived = HandleDataReceived;

			//mainScheduler = TaskScheduler.FromCurrentSynchronizationContext ();

			isRunning = true;
			return true;
		}

		void ResetIDE ()
		{
			client.Send (new ResetMessage ());
		}

		string GetIdeIPFromResource ()
		{
			try {
				using (Stream stream = GetType ().Assembly.GetManifestResourceStream (Constants.IDE_IP_RESOURCE_NAME))
				using (StreamReader reader = new StreamReader (stream)) {
					return reader.ReadToEnd ().Split ('\n') [0].Trim ();
				}
			} catch (Exception ex) {
				Debug.WriteLine (ex);
				return null;
			}
		}

		async void HandleDataReceived (object e)
		{
			var container = e as JContainer;
			string type = (string)container ["Type"];

			if (type == typeof (EvalRequestMessage).Name) {
				await HandleEvalRequest (container.ToObject<EvalRequestMessage> ());
			} else if (type == typeof (ErrorMessage).Name) {
				//var errorMessage = container.ToObject<ErrorMessage>();
				//await uiToolkit.RunInUIThreadAsync(async () =>
				//{
				//	errorViewModel.SetError("Oh no! An exception!", errorMessage.Exception);
				//	await previewer.NotifyError(errorViewModel);
				//});
			}
		}
		public const BindingFlags ALL_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
											   BindingFlags.Static | BindingFlags.Instance |
											   BindingFlags.FlattenHierarchy;

		public const BindingFlags ALL_DECLARED_METHODS_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
																BindingFlags.Static | BindingFlags.Instance |
																BindingFlags.DeclaredOnly;

		public readonly List<Type> ExcludeMethodsDefinedOnTypes = new List<Type>
		{
            typeof(System.Object)
		};
		Dictionary<string, Type> previousReplacement = new();
		Dictionary<string, Type> originalType = new();

		Type GetOriginalType(string className)
		{
			if (originalType.TryGetValue(className, out var type))
				return type;
			type = Type.GetType(className);
			if (type == null)
			{
				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					//Lets start without using emit namespaces
					if (assembly.FullName.StartsWith("Reloadify-emit"))
						continue;
					type = assembly.GetType(className);
					if (type != null)
						return type;
				}
			}
			return type;
		}

		protected virtual void HarmonyReplaceType(string className, Type newType)
		{

			try
			{
				Console.WriteLine($"HotReloaded!: {className} - {newType}");

				foreach (var prop in newType.GetProperties())
				{
					Console.WriteLine($"\t{prop.Name}");
				}
				Type oldClass = GetOriginalType(className);
				if (oldClass != null)
				{
					var oldMethods = oldClass.GetMethods(Reload.ALL_DECLARED_METHODS_BINDING_FLAGS);
					var newMethods = newType.GetMethods(Reload.ALL_DECLARED_METHODS_BINDING_FLAGS);
					var allDeclaredMethodsInExistingType = oldClass.GetMethods(Reload.ALL_DECLARED_METHODS_BINDING_FLAGS)
									.Where(m => !Reload.Instance.ExcludeMethodsDefinedOnTypes.Contains(m.DeclaringType))
									.ToList();
					foreach (var method in newMethods)
					{
						Console.WriteLine($"Method: {method.Name}");
						var oldMethod = oldMethods.FirstOrDefault(m => m.Name == method.Name);
						if (oldMethod != null && !method.IsGenericMethod)
						{
							//Found the method. Lets Monkey patch it
							Harmony.DetourMethod(oldMethod, method);
						}
						else
						{
							//Lets not worry about it for now
						}

					}
				}
				else
				{
					//This must be a new type, not in the original 
					Console.WriteLine($"Found a new type: {className}");
					originalType[className] = newType;
				}
				//Call static init if it exists on new classes!
				var staticInit = newType.GetMethod("Init", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
				if (staticInit != null)
				{
					Console.WriteLine($"Calling static Init on :{newType}");
					staticInit?.Invoke(null, null);
					Console.WriteLine("Init completed");
				}

				//Lets copy over the default Values
				if (previousReplacement.TryGetValue(className, out var oldType))
				{
					var oldFieldsProperty = oldType.GetField("__ReloadifyNewFields__", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
					var newFieldsProperty = newType.GetField("__ReloadifyNewFields__", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
					if (oldFieldsProperty != null && newFieldsProperty != null)
					{
						var oldValues = oldFieldsProperty.GetValue(null);
						if(oldValues != null)
							newFieldsProperty.SetValue(null, oldValues);
					}
				}
				previousReplacement[className] = newType;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
		}

		async Task HandleEvalRequest (EvalRequestMessage request)
		{
			Debug.WriteLine ($"Handling request");
			EvalResponse evalResponse = new EvalResponse ();
			EvalResult result = new EvalResult ();
			try {
				//var s = await eval.EvaluateCode (request, result);
				//Debug.WriteLine ($"Evaluating: {s} - {result.FoundClasses.Count}");
				if(!request.Classes?.Any() ?? false)
				{
					//Nothing to load
					return;
				}
				var assmebly = Assembly.Load(request.Assembly, request.Pdb);
				var foundTypes = new List<(string, Type)>();
				foreach(var c in request.Classes)
				{
					var fullName = string.IsNullOrWhiteSpace(c.NameSpace) ? c.ClassName : $"{c.NameSpace}.{c.ClassName}";
					var type = assmebly.GetType(fullName);
					foundTypes.Add((fullName, type));
				}
				if (!foundTypes.Any())
					return;
				StartingReload?.Invoke();
				foreach (var f in foundTypes)
				{
					try
					{
						if (!DisableHarmonyIntegration)
						{
							HarmonyReplaceType(f.Item1, f.Item2);
						}
						ReplaceType?.Invoke(f);
					}
					catch (Exception e)
					{
						Debug.WriteLine(e);
					}
				}
				FinishedReload?.Invoke();
				
			} catch (Exception ex) {
				Debug.WriteLine (ex);
			}
		}

		internal static string Replace (string code, Dictionary<string, string> replaced)
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
	}
}
