using System;

namespace Xamarin.HotReload.Telemetry
{
	public static class TelemetryEvents
	{
		/// <summary>
		/// The prefix to use for all Hot Reload events.
		/// </summary>
		public const string Prefix = "vs/xamarin/hot-reload-hotui/";

		/// <summary>
		/// The prefix (added to above prefix) to use for all Hot Reload agent events.
		/// </summary>
		public const string AgentPrefix = "agent/";

		// Event names WITHOUT prefixes:
		//  We add the above prefixes automatically as necessary.
		//  The benefit of that is that we can log telemetry from either the IDE
		//   or the agent indescriminately, but they will be logged as distinct events.

		/// <summary>
		/// A user task scope that encompasses the entire hot reload session.
		/// </summary>
		public const string Session = "session";

		/// <summary>
		/// An operation sub-scope that involves just the session startup.
		/// </summary>
		public const string SessionInit = "session/init";

		/// <summary>
		/// Used to report failure during session init.
		/// </summary>
		public const string SessionFailed = "session/failed";

		/// <summary>
		/// An operation that encompasses initializing each child agent
		/// </summary>
		public const string Init = "init";

		/// <summary>
		/// Used to report failure.
		/// </summary>
		public const string Failed = "failed";

		/// <summary>
		/// A user task scope that encompasses a single reload.
		/// </summary>
		public const string Reload = "reload";

		/// <summary>
		/// Used to report exception during reload.
		/// </summary>
		public const string ReloadFault = "reload/fault";
	}
}
