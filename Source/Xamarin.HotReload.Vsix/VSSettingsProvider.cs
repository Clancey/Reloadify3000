using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using Xamarin.HotReload.Ide;
using VsServiceProvider = Microsoft.VisualStudio.Shell.ServiceProvider;

namespace Xamarin.HotReload.Vsix
{
	[Export (typeof (ISettingsProvider))]
	public class VSSettingsProvider : ISettingsProvider
	{
		public VSSettingsProvider()
		{
		}

		const string COLLECTION_NAME = "XamarinHotReloadVSIX";

		public event EventHandler SettingsChanged;

		public static bool GetBool(string key, bool defaultValue)
		{
			var settingsManager = new ShellSettingsManager (VsServiceProvider.GlobalProvider);
			var settingsStore = settingsManager.GetReadOnlySettingsStore (Microsoft.VisualStudio.Settings.SettingsScope.UserSettings);

			if (!settingsStore.CollectionExists (COLLECTION_NAME))
				return defaultValue;

			if (!settingsStore.PropertyExists (COLLECTION_NAME, key))
				return defaultValue;

			return settingsStore.GetBoolean (COLLECTION_NAME, key, defaultValue);
		}
		
		public void SetBool (string key, bool value)
		{
			var settingsManager = new ShellSettingsManager (VsServiceProvider.GlobalProvider);
			var settingsStore = settingsManager.GetWritableSettingsStore (Microsoft.VisualStudio.Settings.SettingsScope.UserSettings);

			if (!settingsStore.CollectionExists (COLLECTION_NAME))
				settingsStore.CreateCollection (COLLECTION_NAME);

			settingsStore.SetBoolean (COLLECTION_NAME, key, value);

			SettingsChanged?.Invoke (this, new EventArgs ());
		}

		const string HOT_RELOAD_ENABLED_KEY = "HotReloadEnabled";

		bool? hotReloadEnabled;

		public bool HotReloadEnabled {
			get {
				if (!hotReloadEnabled.HasValue)
					hotReloadEnabled = GetBool (HOT_RELOAD_ENABLED_KEY, true);
				return hotReloadEnabled.Value;
			}
			set {
				hotReloadEnabled = value;
				SetBool (HOT_RELOAD_ENABLED_KEY, value);
			}
		}
	}
}
