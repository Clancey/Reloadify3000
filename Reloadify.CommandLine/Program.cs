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
				{ "p|Platform=", "Platform (AnyCPU, iPhone, iPhoneSimulator)", x => platform = x },
				{ "t|target=", "TargetFramework (net6.0-ios,net6.0-android,net6.0-maccatalyst) ", x => flavor = x },
				{ "c|configuration=", "Configuration (Debug, Release)", x => configuration = x },
				{ "f|folder=", "Root folder for the solution (Defaults to the CSProj Folder)", x=> rootFolder = x },
				{ "h|help", "show this message and exit", h => shouldShowHelp = h != null },
			};


			List<string> extra;
			try
			{
				// parse the command line
				extra = options.Parse(args);
				if (string.IsNullOrWhiteSpace(rootFolder) && !string.IsNullOrWhiteSpace(csProj))
					rootFolder = GetRootDirectory(csProj);
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
				if (!string.IsNullOrWhiteSpace(flavor))
				{
					RoslynCodeManager.Shared.ProjectFlavor = flavor;
				}
				await IDE.Shared.LoadProject(rootFolder, csProj, configuration, platform);
				Console.WriteLine($"{flavor} - {configuration} - {platform}");
				Console.WriteLine($"Activating HotReload");
				Console.WriteLine($"Watching: {rootFolder}");
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
		static string GetRootDirectory(string csProjPath){
			try{
				var root = Path.GetDirectoryName(csProjPath);
				if(!string.IsNullOrWhiteSpace(root))
					return root;

			}
			catch(Exception ex){
				Console.WriteLine(ex.Message);
			}
			return Directory.GetCurrentDirectory();

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
