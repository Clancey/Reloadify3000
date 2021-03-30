using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Esp.Resources;
[assembly: InternalsVisibleTo("Reloadify-emit")]
namespace ReloadifySample
{
	class Program
	{
		static bool shouldClose = false;
		static async Task Main(string[] args)
		{
			Console.WriteLine("Hello World!");

			await Task.Delay(1000);

			await Task.Run(()=>RunHotReload());

			await Task.Run(() =>
			{
				while (!shouldClose)
				{
					var text = Console.ReadLine();
					if (text == "exit")
						shouldClose = true;
				}
			});
			Console.WriteLine("Goodbye");
		}

		internal static void FooBar()
		{
			Console.WriteLine("Foo Bar was Called!");
		}

		static async void RunHotReload(string ideIP = null, int idePort = Constants.DEFAULT_PORT)
		{
			Reloadify.Reload.Instance.ReplaceType = (d) => {
				Console.WriteLine($"HotReloaded: {d.ClassName} -{d.Type}");
				foreach(var prop in d.Type.GetProperties())
				{
					Console.WriteLine($"\t{prop.Name}");
				}
				//Call static init if it exists on new classes!
				var staticInit = d.Type.GetMethod("Init", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
				if(staticInit != null)
				{
					Console.WriteLine($"Calling static Init on :{d.Type}");
					staticInit?.Invoke(null, null);
					Console.WriteLine("Init completed");
				}
			};
			var loaded = await Reloadify.Reload.Init(ideIP, idePort);
			Console.WriteLine($"Hot Reload Initialized: {loaded}");
		}
	}
}
