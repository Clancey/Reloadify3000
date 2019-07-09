using System;
using System.Collections.Generic;

namespace Xamarin.HotReload
{
	[Serializable]
	public sealed class ReloadResult
	{
		/// <summary>
		/// Returns <c>true</c> if this type of change is supported by the agent
		///	  (even if it cannot be completely applied).
		/// </summary>
		public bool IsChangeTypeSupported { get; }

		IReadOnlyCollection<RudeEdit> rudeEdits;
		public IReadOnlyCollection<RudeEdit> RudeEdits => rudeEdits ?? Array.Empty<RudeEdit> ();

		public ReloadResult (bool supported, IReadOnlyCollection<RudeEdit> rudeEdits = null)
		{
			IsChangeTypeSupported = supported;
			this.rudeEdits = rudeEdits;
		}

		/// <summary>
		/// This type of change is not supported by the agent and no action was taken.
		/// </summary>
		public static ReloadResult NotSupported { get; } = new ReloadResult (supported: false);

		/// <summary>
		/// This type of change is supported by the agent, and some or all of the
		///  change may have been applied.
		/// </summary>
		/// <param name="rudeEdits">Edits that were not applied.</param>
		public static ReloadResult Supported (params RudeEdit [] rudeEdits)
			=> new ReloadResult (true, rudeEdits);
	}
}
