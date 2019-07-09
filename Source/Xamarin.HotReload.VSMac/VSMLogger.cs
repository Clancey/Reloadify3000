using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MDLogLevel = MonoDevelop.Core.Logging.LogLevel;

namespace Xamarin.HotReload.VSMac
{
	[Export (typeof (ILogger))]
	public class VSMLogger : ILogger, IDisposable
	{
		public VSMLogger ()
		{
		}

		public static OutputProgressMonitor ProgressMonitor;

		public void Log (LogMessage message)
		{
			LoggingService.Log (GetMDLogLevel (message.Level), message.Message);

			if (message.Level < LogLevel.Info && !Debugger.IsAttached)
				return;

			ProgressMonitor?.Log?.WriteLine (message);
		}

		static MDLogLevel GetMDLogLevel (LogLevel level)
		{
			switch (level) {

			case LogLevel.Debug:
			case LogLevel.Perf: return MDLogLevel.Debug;
			case LogLevel.Info: return MDLogLevel.Info;
			case LogLevel.Warn: return MDLogLevel.Warn;
			case LogLevel.Error: return MDLogLevel.Error;
			case LogLevel.Fail: return MDLogLevel.Fatal;
			}
			return MDLogLevel.Info;
		}

		public void Dispose()
		{
		}
	}
}
