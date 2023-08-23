using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Esp.Resources;
[assembly: InternalsVisibleTo("Reloadify-emit")]
namespace ReloadifySample
{
	class Program
	{
		public static Class1 C = new Class1();
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
			};
			var loaded = await Reloadify.Reload.Init(ideIP, idePort);
			Console.WriteLine($"Hot Reload Initialized: {loaded}");
		}
	}
}
