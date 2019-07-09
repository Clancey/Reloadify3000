using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Xamarin.HotReload.Ide;

namespace Xamarin.HotReload.VSMac
{
	[Export (typeof (IInfoBarProvider))]
	class VSInfoBarProvider : IInfoBarProvider
	{
		[Import]
		internal Lazy<JoinableTaskContext> JoinableTaskContext;

		public Task CloseAsync (string infoBarId)
		{
			return Task.CompletedTask;
		}

		public async Task<string> ShowAsync (string text, bool closable = true, string actionText = null, Func<Task> action = null, string infoBarId = "")
		{
			await JoinableTaskContext.Value.Factory.SwitchToMainThreadAsync ();

			// VSMac won't show an existing InfoBar if the Id already exists in the Workbench.
			// So we can pass it into ShowInfoBar, and it shouldn't rerender if it already exists.
			var id = !string.IsNullOrEmpty(infoBarId) ? infoBarId : Guid.NewGuid ().ToString ();

			var ibopt = new MonoDevelop.Ide.Gui.Components.InfoBarOptions (text) {
				Id = id,
				Items = new MonoDevelop.Ide.Gui.Components.InfoBarItem[0]
			};

			MonoDevelop.Ide.IdeApp.Workbench.ShowInfoBar (false, ibopt);
			return id;
		}
	}
}