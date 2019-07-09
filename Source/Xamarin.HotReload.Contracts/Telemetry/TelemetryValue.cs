using System;

namespace Xamarin.HotReload.Telemetry
{
	[Serializable]
	public abstract class TelemetryValue
	{
		// Private ctor; closed class hierarchy
		TelemetryValue ()
		{
		}

		/// <summary>
		/// A metric, such as a perf timing.
		/// </summary>
		[Serializable]
		public sealed class Metric : TelemetryValue
		{
			public double Value { get; }
			internal Metric (double value) => Value = value;
			public override bool Equals (object obj) => Value.Equals (obj);
			public override int GetHashCode () => Value.GetHashCode ();
		}

		/// <summary>
		/// Personally-identifiable information to be stored hashed.
		/// </summary>
		[Serializable]
		public sealed class Pii : TelemetryValue
		{
			public string Value { get; }
			internal Pii (string value) => Value = value ?? throw new ArgumentNullException (nameof (value));
			public override bool Equals (object obj) => Value.Equals (obj);
			public override int GetHashCode () => Value.GetHashCode ();
		}

		/// <summary>
		/// Complex objects are persisted as JSON. They are used for stack traces (JSON list), for instance.
		/// </summary>
		[Serializable]
		public sealed class Complex : TelemetryValue
		{
			public object Value { get; }
			internal Complex (object value) => Value = value ?? throw new ArgumentNullException (nameof (value));
			public override bool Equals (object obj) => Value.Equals (obj);
			public override int GetHashCode () => Value.GetHashCode ();
		}

		/// <summary>
		/// A string value.
		/// </summary>
		[Serializable]
		public sealed class String : TelemetryValue
		{
			public string Value { get; }
			internal String (string value) => Value = value ?? throw new ArgumentNullException (nameof (value));
			public override bool Equals (object obj) => Value.Equals (obj);
			public override int GetHashCode () => Value.GetHashCode ();
		}

		// ensure all types participate in equality
		public abstract override bool Equals (object obj);
		public abstract override int GetHashCode ();
		public static bool operator == (TelemetryValue v1, TelemetryValue v2) => Equals (v1, v2);
		public static bool operator != (TelemetryValue v1, TelemetryValue v2) => !Equals (v1, v2);

		public static TelemetryValue CreateMetric (double value) => new Metric (value);

		public static TelemetryValue CreatePii (string value) => (value is null)? null : new Pii (value);

		public static TelemetryValue CreateComplex (object value) => (value is null)? null : new Complex (value);

		public static implicit operator TelemetryValue (string value) => (value is null)? null : new String (value);
	}
}
