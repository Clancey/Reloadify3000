using System;
using System.ComponentModel.Composition;
using MonoDevelop.Core;
using Xamarin.HotReload.Ide;

namespace Xamarin.HotReload.VSMac
{
	[Export(typeof(ISettingsProvider))]
	public class VSMSettingsProvider : ISettingsProvider
	{
		public VSMSettingsProvider() { }

		const string ADDIN_KEY = "XamarinHotReload";

		public bool GetBool(string key, bool defaultValue)
			=> PropertyService.Get<bool> ($"{ADDIN_KEY}.{key}", defaultValue);

		public void SetBool (string key, bool value)
		{
			PropertyService.Set ($"{ADDIN_KEY}.{key}", value);
			PropertyService.SaveProperties ();

			SettingsChanged?.Invoke (this, new EventArgs ());
		}

		public event EventHandler SettingsChanged;

		const string HOT_RELOAD_ENABLED_KEY = "HotReloadEnabled";

		bool? hotReloadEnabled = default;

		public bool HotReloadEnabled {
			get {
				if (!hotReloadEnabled.HasValue)
					hotReloadEnabled = GetBool (HOT_RELOAD_ENABLED_KEY, true);
				return hotReloadEnabled.Value;
			}
			set {
				SetBool (HOT_RELOAD_ENABLED_KEY, value);
				hotReloadEnabled = value;
			}
		}
	}
}
