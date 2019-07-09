using System;
using System.Linq;
using System.Collections.Generic;

using Xamarin.HotReload.Telemetry;

namespace Xamarin.HotReload
{
	public class AgentTelemetryService : ITelemetryService
	{
		public bool Enabled => true;

		public void Post (TelemetryEventType eventType, string eventName,
			TelemetryResult result = TelemetryResult.None,
			object correlation = null,
			params (string key, TelemetryValue value) [] properties)
		{
			HotReloadAgent.SendToIde (new PostTelemetryMessage {
				EventType = eventType,
				EventName = eventName,
				Result = result,
				Correlation = correlation as Guid?,
				Properties = properties
			});
		}

		public void ReportException (string eventName, Exception ex,
			object correlation = null,
			params (string key, TelemetryValue value) [] properties)
		{
			HotReloadAgent.SendToIde (new PostTelemetryMessage {
				EventType = TelemetryEventType.Fault,
				EventName = eventName,
				Exception = ex,
				Correlation = correlation as Guid?,
				Properties = properties
			});
		}

		public ITelemetryScope Start (TelemetryEventType eventType, string eventName,
			object correlation = null,
			params (string key, TelemetryValue value) [] properties)
		{
			var scope = new Scope ();
			HotReloadAgent.SendToIde (new PostTelemetryMessage {
				EventType = eventType,
				EventName = eventName,
				Correlation = correlation as Guid?,
				Properties = properties,
				ScopeCorrelation = (Guid)scope.Correlation
			});
			return scope;
		}

		class Scope : ITelemetryScope
		{
			Dictionary<string,TelemetryValue> properties;

			public object Correlation { get; } = Guid.NewGuid ();

			public void AddProperty (string key, TelemetryValue value)
			{
				if (properties is null)
					properties = new Dictionary<string,TelemetryValue> ();
				properties [key] = value;
			}

			public void AddProperties ((string, TelemetryValue) [] props)
			{
				if (properties is null)
					properties = new Dictionary<string,TelemetryValue> ();
				foreach (var (key, value) in props)
					properties [key] = value;
			}

			public void End (TelemetryResult result, string resultMessage = null)
			{
				HotReloadAgent.SendToIde (new EndTelemetryScopeMessage {
					ScopeCorrelation = (Guid)Correlation,
					Properties = properties?.Select (kv => (kv.Key, kv.Value)).ToArray (),
					Result = result,
					ResultMessage = resultMessage
				});
			}
		}
	}
}
