using System;

namespace Xamarin.HotReload
{
	[Serializable]
	public class ReloadTransaction
	{
		public ReloadChange Change { get; }
		public ReloadResult Result { get; set; } = ReloadResult.NotSupported;

		public ReloadTransaction (ReloadChange change)
			=> Change = change ?? throw new ArgumentNullException (nameof (change));
	}
}
