using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using Reloadify;
using Timer = System.Timers.Timer;

namespace Reloadify.CommandLine
{
	public class FileWatcher : IDisposable
	{
		FileSystemWatcher fileWatcher;
		private bool disposedValue;

		public FileWatcher(string filePath)
		{
			fileWatcher = new FileSystemWatcher(filePath)
			{
				Filter = "*.cs",
				IncludeSubdirectories = true,
			};
			
			// Visual Studio (on Windows) renames files before "saving" which causes the "file change" not fire
			// this NotifyFilter remedies that
			fileWatcher.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite;
			fileWatcher.Changed += FileWatcher_Changed;
			fileWatcher.Created += FileWatcher_Created;
			fileWatcher.Deleted += FileWatcher_Deleted;
			fileWatcher.Renamed += FileWatcher_Renamed;
			fileWatcher.Error += FileWatcher_Error;
			fileWatcher.EnableRaisingEvents = true;

		}

		void FileWatcher_Error(object sender, ErrorEventArgs e) =>
			PrintException(e.GetException());

		void FileWatcher_Renamed(object sender, RenamedEventArgs e)
		{
			// Visual Studio (on Windows) makes this fire on files not ending with ".cs" even though we have specified that in the FileSystemWatcher
			// ignore all others file types
			if (!e.Name.EndsWith(".cs", StringComparison.Ordinal))
				return;

			RoslynCodeManager.Shared.Rename(e.OldFullPath, e.FullPath);
		}


		void FileWatcher_Deleted(object sender, FileSystemEventArgs e) => RoslynCodeManager.Shared.Delete(e.FullPath);

		void FileWatcher_Created(object sender, FileSystemEventArgs e) => RoslynCodeManager.Shared.NewFiles.Add(e.FullPath);

		List<string> currentfiles = new();
		Timer searchTimer;
	

		static string CleanseFilePath(string filePath)
		{
			//On Mac, it may send // for root paths.
			//MSBuild just has /, so we need to cleanse it
			if (!filePath.StartsWith("//"))
				return filePath;
			return filePath[1..];
		}

		void FileWatcher_Changed(object sender, FileSystemEventArgs e)
		{
			var filePath = CleanseFilePath(e.FullPath);
			if (ShouldExcludePath(filePath))
				return;

			if (!currentfiles.Contains(filePath))
				currentfiles.Add(filePath);
			if (searchTimer == null)
			{
				searchTimer = new Timer(100);
				searchTimer.Elapsed += (s, e) =>
				{
					var files = currentfiles.ToArray();
					currentfiles.Clear();
					foreach(var f in files)
					{
						//Console.WriteLine($"Reading: {f}");
						var fileData = File.ReadAllText(f);
						IDEManager.Shared.HandleDocumentChanged(new DocumentChangedEventArgs(f, fileData));
					}
				};
			}
			else
				searchTimer.Stop();
			searchTimer.Start();
			
		}


		static bool ShouldExcludePath(string path)
		{
			foreach (var dir in excludedDirs)
				if (path.Contains(dir))
					return true;
			return false;
		}
		static char slash => Path.DirectorySeparatorChar;
		static List<string> excludedDirs = new()
		{
			$"{slash}obj{slash}",
			$"{slash}bin{slash}"
		};

		static void PrintException(Exception ex)
		{
			if (ex != null)
			{
				Console.WriteLine($"Message: {ex.Message}");
				Console.WriteLine("Stacktrace:");
				Console.WriteLine(ex.StackTrace);
				Console.WriteLine();
				PrintException(ex.InnerException);
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					fileWatcher?.Dispose();
				}

				disposedValue = true;
			}
		}


		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
