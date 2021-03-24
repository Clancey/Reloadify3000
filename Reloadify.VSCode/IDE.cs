using System;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Reloadify.VSCode
{
	public class IDE
	{
		MSBuildWorkspace currentWorkSpace;
		public static IDE Shared { get; set; } = new IDE();
		public async void LoadProject(string csprojPath)
		{
			using (new MonoHack())
			{
				//var workSpace = MSBuildWorkspace.Create();
				currentWorkSpace ??= MSBuildWorkspace.Create();
				var project = await currentWorkSpace.OpenProjectAsync(csprojPath);
				var sln = project.Solution;
				IDEManager.Shared.Solution = sln;
			}

		}
	}

	public class MonoHack : IDisposable
	{
		private static bool? _isMono;
		public static bool IsMono
		{
			get
			{
				if (!_isMono.HasValue)
				{
					_isMono = Type.GetType("Mono.Runtime") != null;
				}
				return _isMono.Value;
			}
		}

		private const string MSBUILD_EXE_PATH = "MSBUILD_EXE_PATH";
		private readonly string _msbuildExePath;

		public MonoHack()
		{
			if (IsMono)
			{
				//var nativeSharedMethod = typeof(SolutionFile).Assembly.GetType("Microsoft.Build.Shared.NativeMethodsShared");
				//var isMonoField = nativeSharedMethod.GetField("_isMono", BindingFlags.Static | BindingFlags.NonPublic);
				//isMonoField.SetValue(null, true);

				_msbuildExePath = Environment.GetEnvironmentVariable(MSBUILD_EXE_PATH);
				var p = Assembly.GetExecutingAssembly().Location;
				//Environment.SetEnvironmentVariable(MSBUILD_EXE_PATH, "/Users/clancey/.vscode/extensions/ms-dotnettools.csharp-1.23.9/.omnisharp/1.37.6/omnisharp/.msbuild/Current/Bin/MSBuild.exe");
				Environment.SetEnvironmentVariable(MSBUILD_EXE_PATH, "/Applications/Visual Studio.app/Contents/Resources/lib/monodevelop/bin/MSBuild/Current/bin/MSBuild.dll");
			}
		}

		public void Dispose()
		{
			if (IsMono)
			{
				Environment.SetEnvironmentVariable(MSBUILD_EXE_PATH, _msbuildExePath);
			}
		}
	}
}