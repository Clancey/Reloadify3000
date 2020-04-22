using System;
namespace Esp {
	public class Message {
		public string Type => GetType ().Name;
	}
	public class ConnectMessage : Message {
		public string ClientId { get; set; }
	}

	public class DiscconectMessage : Message {
		public string ClientId { get; set; }
	}
}
