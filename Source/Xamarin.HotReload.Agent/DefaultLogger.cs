using System.Diagnostics;

namespace Xamarin.HotReload
{
	public class DefaultLogger : ILogger
	{
		/// <summary>
		/// Sets the minimum <see cref="LogLevel"/> for this logger.
		/// </summary>
		public LogLevel LogLevel { get; set; } = LogLevel.All;

		public virtual void Log (LogMessage message)
		{
			if (message.Level < LogLevel)
				return;

			// FIXME: Send this over the wire, so we can log it separately from the user's app output
			Debug.WriteLine (message);
		}
	}
}
