using System;
using System.Diagnostics;

namespace Xamarin.HotReload.Telemetry
{
	public enum TelemetryEventType
	{
		/// <summary>
		/// A user-initiated event
		/// </summary>
		UserTask,

		/// <summary>
		/// A non-user-initiated event
		/// </summary>
		Operation,

		/// <summary>
		/// A fault
		/// </summary>
		Fault
	}

	public interface ITelemetryService
	{
		bool Enabled { get; }

		/// <summary>
		/// Posts a new telemetry event.
		/// </summary>
		void Post (TelemetryEventType eventType, string eventName,
			TelemetryResult result = TelemetryResult.None,
			object correlation = null,
			params (string key, TelemetryValue value) [] properties);

		/// <summary>
		/// Starts a new <see cref="TelemetryEventType.UserTask"/> or <see cref="TelemetryEventType.Operation"/>
		///  scope that has a start event correlated with an end event.
		/// </summary>
		ITelemetryScope Start (TelemetryEventType eventType, string eventName,
			object correlation = null,
			params (string key, TelemetryValue value) [] properties);

		/// <summary>
		/// Reports a fault with the given exception.
		/// </summary>
		void ReportException (string eventName, Exception ex,
			object correlation = null,
			params (string key, TelemetryValue value) [] properties);
	}

	public static class TelemetryService
	{
		public static ITelemetryService Disabled => new DisabledTelemetryService ();

		public static ITelemetryService WithPrefix (this ITelemetryService svc, string prefix)
			=> new PrefixedTelemetryService (svc, prefix);

		class DisabledTelemetryService : ITelemetryService
		{
			public bool Enabled => false;

			public void Post (TelemetryEventType eventType, string eventName,
				TelemetryResult result = TelemetryResult.None,
				object correlation = null,
				params (string key, TelemetryValue value) [] properties)
			{
				Debug.WriteLine ($"[TELEMETRY] {nameof(Post)} ({eventType}, {eventName}, result: {result}, correlation: {correlation}, properties: {properties})");
			}

			public ITelemetryScope Start (TelemetryEventType eventType, string eventName,
				object correlation = null,
				params (string key, TelemetryValue value) [] properties)
			{
				Debug.WriteLine ($"[TELEMETRY] {nameof (Start)} ({eventType}, {eventName}, correlation: {correlation}, properties: {properties})");
				return new NullTelemetryScope ();
			}

			public void ReportException (string eventName, Exception ex,
				object correlation = null,
				params (string key, TelemetryValue value) [] properties)
			{
				Debug.WriteLine ($"[TELEMETRY] {nameof(ReportException)} ({eventName}, {ex.GetType ().FullName}, correlation: {correlation}, properties: {properties})");
			}
		}

		class PrefixedTelemetryService : ITelemetryService
		{
			ITelemetryService parent;
			string prefix;

			public bool Enabled => parent.Enabled;

			public PrefixedTelemetryService (ITelemetryService parent, string prefix)
			{
				this.parent = parent ?? throw new ArgumentNullException (nameof (parent));
				this.prefix = prefix.EndsWith ("/", StringComparison.Ordinal)? prefix : prefix + "/";
			}

			public void Post (TelemetryEventType eventType, string eventName,
				TelemetryResult result = TelemetryResult.None,
				object correlation = null,
				params (string key, TelemetryValue value) [] properties)
			{
				parent.Post (eventType, prefix + eventName, result, correlation, properties);
			}

			public ITelemetryScope Start (TelemetryEventType eventType, string eventName,
				object correlation = null,
				params (string key, TelemetryValue value) [] properties)
			{
				return parent.Start (eventType, prefix + eventName, correlation, properties);
			}

			public void ReportException (string eventName, Exception ex,
				object correlation = null,
				params (string key, TelemetryValue value) [] properties)
			{
				parent.ReportException (prefix + eventName, ex, correlation, properties);
			}
		}
	}
}
