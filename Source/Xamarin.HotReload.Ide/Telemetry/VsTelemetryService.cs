using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.Telemetry;
using Xamarin.HotReload.Telemetry;
using static Xamarin.HotReload.LogLevel;

namespace Xamarin.HotReload.Ide
{
	using HrTelemetryResult = Xamarin.HotReload.Telemetry.TelemetryResult;
	using TelemetryService = Microsoft.VisualStudio.Telemetry.TelemetryService;

	/// <summary>
	/// Send telemetry using the VS telemetry APIs. 
	/// 
	/// IMPORTANT: Requires Dev15 (or higher)
	/// </summary>
	class VsTelemetryService : ITelemetryService
	{
		ILogger logger;

		public bool Enabled => true;

		public VsTelemetryService (ILogger logger)
		{
			this.logger = logger;
		}

		public void Post (TelemetryEventType eventType, string eventName,
			HrTelemetryResult result = HrTelemetryResult.None,
			object correlation = null,
			params (string key, TelemetryValue value) [] properties)
		{
			TelemetryEvent evt;
			var tr = result.ToVsTelemetryResult (logger);
			switch (eventType) {

			case TelemetryEventType.UserTask: evt = new UserTaskEvent (eventName, tr); break;
			case TelemetryEventType.Operation: evt = new OperationEvent (eventName, tr); break;
			case TelemetryEventType.Fault: evt = new FaultEvent (eventName, string.Empty); break;
			default:
				logger.Log (Warn, $"Unsupported {nameof(TelemetryEventType)}: {eventType}");
				return;
			}
			Post (evt, correlation, properties);
		}

		public void ReportException (string eventName, Exception ex,
			object correlation = null,
			params (string key, TelemetryValue value) [] properties)
		{
			var evt = new FaultEvent (eventName, string.Empty, ex);
			Post (evt, correlation, properties);
		}

		void Post (TelemetryEvent evt, object correlation, (string, TelemetryValue) [] properties)
		{
			try {
				if (evt is FaultEvent fault) {
					// Disable Watson, as this might hang whatever thread we happen to be in,
					//  including the UI thread!
					fault.IsIncludedInWatsonSample = false;
				}

				// Add correlation, if there is one
				if (correlation is TelemetryEventCorrelation tec)
					evt.Correlate (tec);

				// Add properties
				MapProperties (evt.Properties, properties);

				// Post the event
				TelemetryService.DefaultSession.PostEvent (evt);
			} catch (Exception ex) {
				logger.Log (ex);
			}
		}

		public ITelemetryScope Start (TelemetryEventType eventType, string eventName,
			object correlation = null,
			params (string key, TelemetryValue value) [] properties)
		{
			try {
				switch (eventType) {

				case TelemetryEventType.UserTask: {
					var scope = TelemetryService.DefaultSession.StartUserTask (eventName, GetScopeSettings (correlation, properties));
					return new Scope<UserTaskEvent> (this, scope);
				}

				case TelemetryEventType.Operation: {
					var scope = TelemetryService.DefaultSession.StartOperation (eventName, GetScopeSettings (correlation, properties));
					return new Scope<OperationEvent> (this, scope);
				}

				default:
					logger.Log (Warn, $"Unsupported {nameof (TelemetryEventType)}: {eventType}");
					break;
				}
			} catch (Exception ex) {
				logger.Log (ex);
			}
			return new Scope<UserTaskEvent> (this, null);
		}

		TelemetryScopeSettings GetScopeSettings (object correlation, (string, TelemetryValue) [] properties)
		{
			var result = new TelemetryScopeSettings ();
			if (correlation is TelemetryEventCorrelation tec)
				result.Correlations = new[] { tec };

			if (!(properties is null) && properties.Length > 0) {
				var vsProps = new Dictionary<string, object> (properties.Length);
				MapProperties (vsProps, properties);
				result.StartEventProperties = vsProps;
			}
			return result;
		}

		void MapProperties (IDictionary<string, object> dest, params (string, TelemetryValue) [] src)
		{
			foreach (var (key, value) in src) {
				switch (value) {

				// ignore null value
				case null:
					break;

				case TelemetryValue.Metric metric:
					dest.Add (key, new TelemetryMetricProperty (metric.Value));
					break;

				case TelemetryValue.Pii pii:
					dest.Add (key, new TelemetryPiiProperty (pii.Value));
					break;

				case TelemetryValue.Complex complex:
					dest.Add (key, new TelemetryComplexProperty (complex.Value));
					break;

				case TelemetryValue.String str:
					dest.Add (key, str.Value);
					break;

				default:
					logger.Log (Warn, $"Unsupported type of {nameof (TelemetryValue)}: {value}");
					break;
				}
			}
		}

		class Scope<T> : ITelemetryScope where T : OperationEvent
		{
			readonly VsTelemetryService parent;
			readonly TelemetryScope<T> scope; // may be null

			public object Correlation => scope?.Correlation;

			public Scope (VsTelemetryService parent, TelemetryScope<T> scope)
			{
				this.parent = parent;
				this.scope = scope;
			}

			public void AddProperty (string key, TelemetryValue value)
			{
				if (scope is null)
					return;
				parent.MapProperties (scope.EndEvent.Properties, (key, value));
			}

			public void AddProperties ((string, TelemetryValue) [] properties)
			{
				if (scope is null)
					return;
				parent.MapProperties (scope.EndEvent.Properties, properties);
			}

			public void End (HrTelemetryResult result, string resultMessage = null)
			{
				try {
					var tr = result.ToVsTelemetryResult (parent.logger);
					scope?.End (tr, resultMessage);
				} catch (Exception ex) {
					parent.logger.Log (ex);
				}
			}
		}
	}
}