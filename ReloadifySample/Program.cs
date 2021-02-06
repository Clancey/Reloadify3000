using System;
using System.Threading.Tasks;
using Esp.Resources;

namespace ReloadifySample
{
	class Program
	{
		static bool shouldClose = false;
		static async Task Main(string[] args)
		{
			Console.WriteLine("Hello World!");

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

		static async void RunHotReload(string ideIP = null, int idePort = Constants.DEFAULT_PORT)
		{
			Reloadify.Reload.Instance.ReplaceType = (d) => {
				Console.WriteLine($"HotReloaded: {d.ClassName} -{d.Type}");
				foreach(var prop in d.Type.GetProperties())
				{
					Console.WriteLine($"\t{prop.Name}");
				}
			};
			var loaded = await Reloadify.Reload.Init(ideIP, idePort);
			Console.WriteLine($"Hot Reload Initialized: {loaded}");
		}
	}
}
