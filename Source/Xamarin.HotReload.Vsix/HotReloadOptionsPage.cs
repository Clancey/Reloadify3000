using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using Xamarin.HotReload.Ide;

namespace Xamarin.HotReload.Vsix
{
	[Guid ("9315EA07-9D02-4B3E-85D9-0488CF541365")]
	public class HotReloadOptionsPage : DialogPage
	{
		ISettingsProvider settings;
		HotReloadOptionsControl page;

		public HotReloadOptionsPage ()
		{
			settings = CompositionManager.GetExportedValue<ISettingsProvider> ();
			page = new HotReloadOptionsControl ();
		}

		public override void SaveSettingsToStorage ()
		{
			base.SaveSettingsToStorage ();

			settings.HotReloadEnabled = page.HotReloadEnabled;
		}

		public override void LoadSettingsFromStorage ()
		{
			base.LoadSettingsFromStorage ();

			if (page == null)
				page = new HotReloadOptionsControl ();

			page.HotReloadEnabled = settings.HotReloadEnabled;
		}

		protected override IWin32Window Window {
			get {
				if (page == null)
					page = new HotReloadOptionsControl ();
				return page;
			}
		}
	}
}
