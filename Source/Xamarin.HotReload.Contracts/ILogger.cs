using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Xamarin.HotReload
{
	// in order of severity..
	public enum LogLevel
	{
		/// <summary>
		/// Used for filtering only; All messages are logged
		/// </summary>
		All, // must be first

		/// <summary>
		/// Informational messages used for debugging or to trace code execution
		/// </summary>
		Debug,

		/// <summary>
		/// Informational messages containing performance metrics
		/// </summary>
		Perf,

		/// <summary>
		/// Informational messages that might be of interest to the user
		/// </summary>
		Info,

		/// <summary>
		/// Warnings
		/// </summary>
		Warn,

		/// <summary>
		/// Errors that are handled gracefully
		/// </summary>
		Error,

		/// <summary>
		/// Errors that are not handled gracefully
		/// </summary>
		Fail,

		/// <summary>
		/// Used for filtering only; No messages are logged
		/// </summary>
		None, // must be last
	}

	[Serializable]
	public sealed class LogMessage
	{
		const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.f";

		public DateTime Timestamp { get; }
		public LogLevel Level { get; }
		public string Message { get; private set; }

		public LogMessage (DateTime timestamp, LogLevel level, string message)
		{
			if (level <= LogLevel.All || level >= LogLevel.None)
				throw new ArgumentException ("Invalid log level", nameof (level));
			if (message is null)
				throw new ArgumentNullException (nameof (message));
			Timestamp = timestamp;
			Level = level;
			Message = message;
		}

		public LogMessage WithMessage (string message)
		{
			var result = (LogMessage)MemberwiseClone ();
			result.Message = message;
			return result;
		}

		public override string ToString ()
			=> $"[HotReload] ({Timestamp.ToString (TimestampFormat)}): {Level.ToString ().ToUpperInvariant ()}: {Message}";
	}

	public interface ILogger
	{
		void Log (LogMessage message);
	}

	public static class Logger
	{
		public static void Log (this ILogger logger, LogLevel level, string message)
			=> logger.Log (new LogMessage (DateTime.Now, level, message));

		public static void Log (this ILogger logger,
			Exception ex,
			LogLevel level = LogLevel.Error,
			[CallerMemberName] string memberName = "(unknown)",
			[CallerLineNumber] int sourceLineNumber = 0)
		{
			logger.Log (level, $"Caught exception in {memberName} at {sourceLineNumber}: {ex}\n{ex.StackTrace}");
		}

		public static void LogIfFaulted (this Task task,
			ILogger logger,
			LogLevel level = LogLevel.Error,
			[CallerMemberName] string memberName = "(unknown)",
			[CallerLineNumber] int sourceLineNumber = 0)
		{
			task.ContinueWith (t => logger.Log (t.Exception, level, memberName, sourceLineNumber),
				TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
		}

		public static void Log (this ILogger logger,
			Stopwatch sw,
			LogLevel level = LogLevel.Perf,
			[CallerMemberName] string memberName = "(unknown)",
			[CallerLineNumber] int sourceLineNumber = 0)
		{
			logger.Log (level, $"Elapsed time in {memberName} at {sourceLineNumber}: {sw.ElapsedMilliseconds}ms");
		}

		/// <summary>
		/// Returns a new <see cref="ILogger"/> that prefixes every message with
		///  parenthesis and the given tag.
		/// </summary>
		public static ILogger WithTag (this ILogger logger, string tag)
			=> new TaggedLogger (logger, tag);
	}

	class TaggedLogger : ILogger
	{
		ILogger logger;
		string tag;

		public TaggedLogger (ILogger logger, string tag)
		{
			this.logger = logger;
			this.tag = tag;
		}

		public void Log (LogMessage log)
			=> logger.Log (log.WithMessage ($"({tag}) {log.Message}"));
	}
}
