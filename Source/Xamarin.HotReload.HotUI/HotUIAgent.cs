using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xamarin.HotReload;
using System.Linq;
using static Xamarin.HotReload.LogLevel;
using System.Security.Permissions;
using System.Runtime.Serialization;
using HotUI;

[assembly: HotReloadAgent (typeof (Xamarin.HotReload.HotUI.HotUIAgent))]

namespace Xamarin.HotReload.HotUI
{
	public class HotUIAgent : IHotReloadAgent
	{
		//static readonly Version RequiredXFVersion = new Version ("4.1.0.267");
		public static ILogger Logger { get; private set; }
		public static IFileContentProvider FileProvider { get; private set; }

		public void InitializeAgent (IServiceProvider provider)
		{
			Logger = provider.GetService<ILogger> ();
			Logger.Log (Info, "HotReload: Initializing Agent for HotUI...");

			

			//Logger.Log (Info, $"HotReload: HotUI {formsVersion} installed");

			FileProvider = provider.GetService<IFileContentProvider> ();
			if (FileProvider is null) {
				var msg = "Cannot retrieve content for XAML files";
				Logger.Log (Error, msg);
				throw new InvalidOperationException (msg);
			}

			//try {
			//	//ResourceLoader.ResourceProvider2 = ResourceProvider.GetResourceAndTrackObjects;
			//	//DesignMode.IsDesignModeEnabled = false;
			//	//ResourceLoader.ExceptionHandler2 = ReloadExtensions.OnRecoverableException;
			//} catch (Exception ex) {
			//	Logger.Log (Error, "Failed to initialize the HotReload.Forms Agent");
			//	Logger.Log (ex);
			//	throw ex;
			//}
			Logger.Log (Info, "HotReload: Initialized Agent for HotUI.");
		}

		public async Task ReloadAsync (IEnumerable<ReloadTransaction> requests)
		{
			foreach (var req in requests) {
                if(!string.IsNullOrWhiteSpace(req.Change.File.NewAssembly))
				// FIXME: Build action instead of file extension?
				//if (System.IO.Path.GetExtension (req.Change.File.SourcePath).ToLowerInvariant () == ".cs")
					req.Result = await ReloadFileAsync (req.Change.File);
			}
		}
		Dictionary<string, string> ReloadedDlls = new Dictionary<string, string> ();
		async Task<ReloadResult> ReloadFileAsync (FileIdentity file)
		{
			Logger.Log (LogLevel.Debug, $"Received CS update for '//{file.AssemblyName.Name}/{file.RelativePath}'");

			var sourcePath = file.SourcePath;

			file.SourcePath = file.NewAssembly;
			var newAssembly = await FileProvider.GetContentAsync (file);
            file.SourcePath = sourcePath;
			var temp = System.IO.Path.GetTempFileName ();
			using (var fileStream = File.OpenWrite (temp)) {
				newAssembly.CopyTo (fileStream);
			}
			var sw = new Stopwatch ();
            try
            {
                ///
                ReloadedDlls[sourcePath] = temp;

                var assembly = Assembly.LoadFile(temp);

                Logger.Log(LogLevel.Debug, $"HotUI Replacing Classes: {file?.Classes?.Count ?? 0}");
                foreach (var pair in file.Classes)
                {
                    Logger.Log(LogLevel.Debug, $"HotUI Replaceing new Types: {pair.Key} - {pair.Value}");
                    var t = assembly.GetType(pair.Value);
                    Logger.Log(LogLevel.Debug, $"HotUI Replaceing new Types: {pair.Key} - {t.FullName}");
                    HotReloadHelper.RegisterReplacedView(pair.Key, t);
                }
                Logger.Log(LogLevel.Debug, $"HotUI Triggering Reload");
                HotReloadHelper.TriggerReload();
            }
            catch(Exception ex)
            {

                Logger.Log(Error, $"HotReload: Error: {ex}");
            }
			///

			Logger.Log (Perf, $"Reloaded Classes for '//{file.AssemblyName.Name}/{file.RelativePath}' in {sw.ElapsedTicks}ms");
			return ReloadResult.Supported();
		}

		//only used for unitTesting
		internal void DeInit ()
		{
		
		}

		//only used for unitTesting
		internal void DisableResilientLoader ()
		{

		}
	}
	
}
