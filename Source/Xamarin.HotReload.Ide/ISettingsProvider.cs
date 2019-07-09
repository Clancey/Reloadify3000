using System;

namespace Xamarin.HotReload.Ide
{
	public interface ISettingsProvider
	{
		event EventHandler SettingsChanged;

		bool HotReloadEnabled { get; set; }
	}
}
