using System;
using System.Diagnostics;
using System.Collections.Generic;

using Xamarin.HotReload.Telemetry;

namespace Xamarin.HotReload.Telemetry
{
	/// <summary>
	/// Wraps an <see cref="ITelemetryService"/> with a scope stack that
	///  automatically correlates anything that doesn't have an explicit
	///  correlation with the scope on the top of the stack.
	/// </summary>
	public class GlobalScopedTelemetryService : ITelemetryService
	{
		ITelemetryService parent;
		Stack<ITelemetryScope> scopes = new Stack<ITelemetryScope> (); // lock!

		public bool Enabled => parent.Enabled;

		object GlobalCorrelation {
			get {
				lock (scopes)
					return scopes.Count > 0 ? scopes.Peek ().Correlation : null;
			}
		}

		public GlobalScopedTelemetryService (ITelemetryService parent)
		{
			this.parent = parent ?? throw new ArgumentNullException (nameof (parent));
		}

		void PushGlobalScope (ITelemetryScope scope)
		{
			lock (scopes)
				scopes.Push (scope);
		}

		ITelemetryScope PopGlobalScope ()
		{
			lock (scopes)
				return scopes.Pop ();
		}

		public void Post (TelemetryEventType eventType, string eventName,
			TelemetryResult result = TelemetryResult.None,
			object correlation = null,
			params (string key, TelemetryValue value) [] properties)
		{
			parent.Post (eventType, eventName, result, correlation ?? GlobalCorrelation, properties);
		}

		public ITelemetryScope Start (TelemetryEventType eventType, string eventName,
			object correlation = null,
			params (string key, TelemetryValue value) [] properties)
		{
			return parent.Start (eventType, eventName, correlation ?? GlobalCorrelation, properties);
		}

		public ITelemetryScope StartGlobal (TelemetryEventType eventType, string eventName,
			object correlation = null,
			params (string key, TelemetryValue value) [] properties)
		{
			var result = Start (eventType, eventName, correlation, properties);
			PushGlobalScope (result);
			return result;
		}

		public void ReportException (string eventName, Exception ex,
			object correlation = null,
			params (string key, TelemetryValue value) [] properties)
		{
			parent.ReportException (eventName, ex, correlation ?? GlobalCorrelation, properties);
		}

		class GlobalTelemetryScope : ITelemetryScope
		{
			GlobalScopedTelemetryService parent;
			ITelemetryScope scope;

			public GlobalTelemetryScope (GlobalScopedTelemetryService parent, ITelemetryScope scope)
			{
				this.parent = parent;
				this.scope = scope;
			}

			public object Correlation => scope.Correlation;

			public void AddProperties ((string, TelemetryValue) [] properties) => scope.AddProperties (properties);
			public void AddProperty (string key, TelemetryValue value) => scope.AddProperty (key, value);

			public void End (TelemetryResult result, string resultMessage = null)
			{
				scope.End (result, resultMessage);
				Debug.Assert (parent.PopGlobalScope () == this);
			}
		}
	}
}
