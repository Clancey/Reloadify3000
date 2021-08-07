using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Mono.Options;

namespace Reloadify.CommandLine
{
	class Program
	{
		static async Task Main(string[] args)
		{
			string platform = "AnyCPU";
			string flavor = "net6.0-ios";
			string configuration = "Debug";
			string rootFolder = "";
			string csProj = args.FirstOrDefault();
			var shouldShowHelp = false;

			var options = new OptionSet {
				{ "p|Platform=", "Platform ", x => platform = x },
				{ "pf|flavor=", "Flavor (net6.0-ios,net6.0-android) ", x => flavor = x },
				{ "c|configuration=", "the number of times to repeat the greeting.", x => configuration = x },
				{ "f|folder=", "Root folder for the solution", x=> rootFolder = x },
				{ "h|help", "show this message and exit", h => shouldShowHelp = h != null },
			};


			List<string> extra;
			try
			{
				// parse the command line
				extra = options.Parse(args);
				if (string.IsNullOrWhiteSpace(rootFolder) && !string.IsNullOrWhiteSpace(csProj))
					rootFolder = Path.GetDirectoryName(csProj);
			}
			catch (OptionException e)
			{
				// output some error message
				Console.Write("Reloadify: ");
				Console.WriteLine(e.Message);
				ShowHelp(options);
				return;
			}

			if (string.IsNullOrWhiteSpace(csProj) || string.IsNullOrWhiteSpace(rootFolder))
			{
				shouldShowHelp = true;
			}

			if (shouldShowHelp)
			{
				ShowHelp(options);
				return;
			}
			try
			{
				await IDE.Shared.LoadProject(rootFolder, csProj, configuration, platform);
				Console.WriteLine($"Activating HotReload");
				bool isHotReloading = await IDE.Shared.StartHotReload();
				if (!isHotReloading)
				{
					Console.WriteLine("Please add Reloadify3000 nuget to your project.");
					return;
				}
				else
				{
					Console.WriteLine($"Hot Reload is running!");
				}

				Console.WriteLine("Type exit, to quit");
				while (true)
				{
					var shouldExit = Console.ReadLine() != "exit";
					if (shouldExit)
					{
						//Shutdown and return;
						return;
					}
				}
			}
			finally
			{
				IDE.Shared.Shutdown();
			}
		}

		private static void ShowHelp(OptionSet p)
		{
			Console.WriteLine("Usage: dotnet run Reloadify.dll <Project> [OPTIONS] ");
			Console.WriteLine();
			Console.WriteLine("Options:");
			p.WriteOptionDescriptions(Console.Out);
		}

	}
}
