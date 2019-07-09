using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using VsServiceProvider = Microsoft.VisualStudio.Shell.ServiceProvider;

namespace Xamarin.HotReload.Vsix
{
	static class CompositionManager
	{
		static IComponentModel ComponentModel
			=> VsServiceProvider.GlobalProvider.GetService (typeof (SComponentModel)) as IComponentModel;

		public static T GetExportedValue<T> () where T: class
			=> ComponentModel.GetService<T> ();
	}
}
