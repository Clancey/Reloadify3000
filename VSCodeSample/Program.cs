using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Mono.Options;
using Reloadify.VSCode;

namespace VSCodeSample
{
	class Program
	{
		static async Task Main(string[] args)
		{
			_ = typeof(Microsoft.Build.FileSystem.MSBuildFileSystemBase);
			string platform = "AnyCPU";
			string configuration = "Debug";
			string rootFolder = "";
			string csProj = args.FirstOrDefault();
			var shouldShowHelp = false;

			var options = new OptionSet {
				{ "p|Platform=", "Platform ", x => platform = x },
				{ "c|configuration=", "the number of times to repeat the greeting.", x => configuration = x },
				{ "f|folder=", "Root folder for the solution", x=> rootFolder = x },
				{ "h|help", "show this message and exit", h => shouldShowHelp = h != null },
			};


			List<string> extra;
			try
			{
				// parse the command line
				extra = options.Parse(args);
			}
			catch (OptionException e)
			{
				// output some error message
				Console.Write("Reloadify: ");
				Console.WriteLine(e.Message);
				ShowHelp(options);
				return;
			}

			if(string.IsNullOrWhiteSpace(csProj) || string.IsNullOrWhiteSpace(rootFolder))
			{
				shouldShowHelp = true;
			}

			if(shouldShowHelp)
			{
				ShowHelp(options);
				return;
			}
			
			await IDE.Shared.LoadProject(rootFolder, csProj,configuration,platform);
			await IDE.Shared.StartHotReload();

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

		private static void ShowHelp(OptionSet p)
		{
			Console.WriteLine("Usage: dotnet run Reloadify.dll <Project> [OPTIONS] ");
			Console.WriteLine();
			Console.WriteLine("Options:");
			p.WriteOptionDescriptions(Console.Out);
		}

	}
}
