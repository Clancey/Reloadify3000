using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Xamarin.HotReload.Vsix
{
	[Export (typeof (ILogger))]
	public class VSLogger : ILogger
	{
		public VSLogger () : base()
		{
		}

		internal static IVsOutputWindowPane VsWindowPane { get; set; }

		static readonly ITracer tracer = Tracer.Get<HotReloadBridge> ();

		public async void Log (LogMessage message)
		{
			tracer.Trace (GetTraceEventType (message), message.Message);

			Debug.WriteLine (message);

			// FIXME: Log to the IDE log here.

			// FIXME: For stable we don't want to show this verbose of logs
			//if (message.Level < LogLevel.Info && !Debugger.IsAttached)
			//	return;

			if (VsWindowPane != null) {
				await ThreadHelper.JoinableTaskFactory
					.SwitchToMainThreadAsync ();

				VsWindowPane?.OutputString (message + Environment.NewLine);
			}
		}

		TraceEventType GetTraceEventType(LogMessage message)
		{
			switch (message.Level) {
			case LogLevel.Debug:
				return TraceEventType.Verbose;
			case LogLevel.Error:
				return TraceEventType.Error;
			case LogLevel.Fail:
				return TraceEventType.Critical;
			case LogLevel.Info:
			case LogLevel.Perf:
				return TraceEventType.Information;
			case LogLevel.None:
				return TraceEventType.Verbose;
			case LogLevel.Warn:
				return TraceEventType.Warning;
			default:
				return TraceEventType.Warning;
			}
		}
	}
}
