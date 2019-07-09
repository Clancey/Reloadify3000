using System;
using Xamarin.HotReload.Telemetry;

namespace Xamarin.HotReload
{
	[Serializable]
	public abstract class Message
	{
	}

	[Serializable]
	public class AsyncResponseMessage : Message
	{
		public long RequestId { get; set; }
		public Exception Exception { get; set; }
		public object Result { get; set; }
	}

	[Serializable]
	public class ViewAppearedMessage : Message
	{
		public FileIdentity File { get; set; }
	}

	[Serializable]
	public class ViewDisappearedMessage : Message
	{
		public FileIdentity File { get; set; }
	}

	[Serializable]
	public class ReloadTransactionMessage : Message
	{
		public ReloadTransaction [] Transactions { get; set; }
	}

	[Serializable]
	public class FileContentRequest : Message
	{
		public long RequestId { get; set; }
		public FileIdentity File { get; set; }
	}

	[Serializable]
	public class AgentStatusMessage : Message
	{
		public uint Version { get; set; }
		public HotReloadState State { get; set; } = HotReloadState.Starting;
		public Exception Exception { get; set; }
	}

	[Serializable]
	public class PostTelemetryMessage : Message
	{
		public TelemetryEventType EventType { get; set; }
		public string EventName { get; set; }
		public Exception Exception { get; set; }
		public TelemetryResult Result { get; set; }
		public Guid? Correlation { get; set; }
		public (string key, TelemetryValue value) [] Properties { get; set; }
		public Guid? ScopeCorrelation { get; set; }
	}

	[Serializable]
	public class EndTelemetryScopeMessage : Message
	{
		public Guid ScopeCorrelation { get; set; }
		public (string key, TelemetryValue value) [] Properties { get; set; }
		public TelemetryResult Result { get; set; }
		public string ResultMessage { get; set; }
	}
}
