using System;
using System.Diagnostics;

namespace Xamarin.HotReload.Telemetry
{
	public interface ITelemetryScope
	{
		object Correlation { get; }
		void AddProperty (string key, TelemetryValue value);
		void AddProperties ((string, TelemetryValue) [] properties);
		void End (TelemetryResult result, string resultMessage = null);
	}

	public class NullTelemetryScope : ITelemetryScope
	{
		public object Correlation => null;

		public void AddProperty (string key, TelemetryValue value)
		{
			Debug.WriteLine ($"[TELEMETRY] Scope.{nameof(AddProperty)} ({key}, {value})");
		}

		public void AddProperties ((string, TelemetryValue) [] properties)
		{
			Debug.WriteLine ($"[TELEMETRY] Scope.{nameof (AddProperties)} ({properties})");
		}

		public void End (TelemetryResult result, string resultMessage = null)
		{
			Debug.WriteLine ($"[TELEMETRY] Scope.{nameof (End)} ({result}, {resultMessage})");
		}
	}
}
