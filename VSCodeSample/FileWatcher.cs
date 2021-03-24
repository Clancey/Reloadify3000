using System;
using System.IO;
using Reloadify;

namespace VSCodeSample
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
				EnableRaisingEvents = true,
			};
			fileWatcher.NotifyFilter = NotifyFilters.Attributes
								 | NotifyFilters.CreationTime
								 | NotifyFilters.DirectoryName
								 | NotifyFilters.FileName
								 | NotifyFilters.LastAccess
								 | NotifyFilters.LastWrite
								 | NotifyFilters.Security
								 | NotifyFilters.Size;
			fileWatcher.Changed += FileWatcher_Changed;
			fileWatcher.Created += FileWatcher_Created;
			fileWatcher.Deleted += FileWatcher_Deleted;
			fileWatcher.Renamed += FileWatcher_Renamed;
			fileWatcher.Error += FileWatcher_Error;

		}

		void FileWatcher_Error(object sender, ErrorEventArgs e) =>
			PrintException(e.GetException());

		void FileWatcher_Renamed(object sender, RenamedEventArgs e) =>
			RoslynCodeManager.Shared.Rename(e.OldFullPath, e.FullPath);


		void FileWatcher_Deleted(object sender, FileSystemEventArgs e) => RoslynCodeManager.Shared.Delete(e.FullPath);

		private void FileWatcher_Created(object sender, FileSystemEventArgs e)
		{
			//Lets ignore created for now. IT won't have any code worth dealing with until its saved anyways
		}

		void FileWatcher_Changed(object sender, FileSystemEventArgs e) =>
			IDEManager.Shared.HandleDocumentChanged(new DocumentChangedEventArgs(e.FullPath, File.ReadAllText(e.FullPath)));


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
