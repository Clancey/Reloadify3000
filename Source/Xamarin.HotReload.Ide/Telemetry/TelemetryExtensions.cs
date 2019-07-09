using System;

using static Xamarin.HotReload.LogLevel;

namespace Xamarin.HotReload.Ide
{
	using HrTelemetryResult = Xamarin.HotReload.Telemetry.TelemetryResult;
	using VsTelemetryResult = Microsoft.VisualStudio.Telemetry.TelemetryResult;

	static class TelemetryExtensions
	{
		public static VsTelemetryResult ToVsTelemetryResult (this HrTelemetryResult result, ILogger logger = null)
		{
			VsTelemetryResult tr;
			switch (result) {
			case HrTelemetryResult.None: tr = VsTelemetryResult.None; break;
			case HrTelemetryResult.Success: tr = VsTelemetryResult.Success; break;
			case HrTelemetryResult.Failure: tr = VsTelemetryResult.Failure; break;
			case HrTelemetryResult.UserFault: tr = VsTelemetryResult.UserFault; break;
			case HrTelemetryResult.UserCancel: tr = VsTelemetryResult.UserCancel; break;
			default:
				logger?.Log (Warn, $"Unexpected TelemetryResult: {result}");
				tr = VsTelemetryResult.None;
				break;
			}
			return tr;
		}
	}
}
