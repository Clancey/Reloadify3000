using System;
namespace HotUI.Internal.Reload {
	public static class Constants
	{
		public const string DEFAULT_HOST = "127.0.0.1";

#if DEBUG
		public const int DEFAULT_PORT = 9988;
#else
		public const int DEFAULT_PORT = 8488;
#endif

		public const string XAMLATOR_ASSEMBLY = "XAMLator.Server.dll";

		public const string IDE_IP_RESOURCE_NAME = "IdeIP";

		public const string ROOT_REPLACEMENT = "@ROOT@";
	}
}
