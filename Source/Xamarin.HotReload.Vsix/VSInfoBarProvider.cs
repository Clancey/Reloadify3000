using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Xamarin.HotReload.Ide;

namespace Xamarin.HotReload.Vsix
{
	[Export (typeof (IInfoBarProvider))]
	class VSInfoBarProvider : IInfoBarProvider, IVsInfoBarUIEvents
	{
		Dictionary<string, InfoBarWrapper> infoBars = new Dictionary<string, InfoBarWrapper> ();

		static readonly object infoBarLocker = new object ();

		public async System.Threading.Tasks.Task CloseAsync (string infoBarId)
		{
			if (infoBars.ContainsKey(infoBarId)) {
				await XamarinHotReloadExtensionPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync ();
				infoBars[infoBarId]?.InfoBarElement?.Close ();
			}
		}

		public async System.Threading.Tasks.Task<string> ShowAsync (string text, bool closable = true, string actionText = null, Func<System.Threading.Tasks.Task> action = null, string infoBarId = "")
		{
			await XamarinHotReloadExtensionPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync ();

			
			var shell = XamarinHotReloadExtensionPackage.Instance.GetService<SVsShell>() as IVsShell;

			if (shell == null)
				return null;

			// Get the main window handle to host our InfoBar
			shell.GetProperty ((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var obj);
			var host = (IVsInfoBarHost)obj;

			//If we cannot find the handle, we cannot do much, so return.
			if (host == null)
				return null;

			var id = !string.IsNullOrEmpty(infoBarId) ? infoBarId : Guid.NewGuid ().ToString ();

			//Construct the InfoBar text span object to host message  sent as a parameter
			var infoBarText = new InfoBarTextSpan (text);

			InfoBarHyperlink infoBarAction = default;

			if (!string.IsNullOrEmpty (actionText) && action != null)
				infoBarAction = new InfoBarHyperlink (actionText, id); // Kinda hack but store the bar id so we can find it later in the action click callback

			// Add the span and actions created above to InfoBarModel.
			// We would also like to show InfoBar as informational (KnwonMonikers.StatusInformation) and we would want it to show Close button.
			var infoBarSpans = new InfoBarTextSpan[] { infoBarText };
			var infoBarActions = infoBarAction != null ? new InfoBarActionItem[] { infoBarAction } : new InfoBarActionItem[0];
			var infoBarModel = new InfoBarModel (infoBarSpans, infoBarActions, Microsoft.VisualStudio.Imaging.KnownMonikers.StatusInformation, isCloseButtonVisible: closable);

			//Get the factory object from IVsInfoBarUIFactory, create it and add it to host.
			var ibsvc = Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider.GetService (typeof (SVsInfoBarUIFactory));
			var factory = ibsvc as IVsInfoBarUIFactory;
			
			var element = factory.CreateInfoBar (infoBarModel);
			element.Advise (this, out uint _cookie);

			host.AddInfoBar (element);

			lock (infoBarLocker)	
				infoBars.Add (id, new InfoBarWrapper { Cookie = _cookie, InfoBarElement = element, Callback = action });

			return id;
		}

		public void OnClosed (IVsInfoBarUIElement infoBarUIElement)
		{
			lock (infoBarLocker) {
				var infoBarId = infoBars.Where (k => k.Value.InfoBarElement == infoBarUIElement)
					.Select (k => k.Key)
					.FirstOrDefault ();

				if (!string.IsNullOrEmpty (infoBarId)) {
					if (infoBars.TryGetValue (infoBarId, out InfoBarWrapper infoBar)) {
						infoBarUIElement.Unadvise (infoBar.Cookie);
						infoBars.Remove (infoBarId);
					}						
				}
			}
		}

		public async void OnActionItemClicked (IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
		{
			Func<System.Threading.Tasks.Task> callback = null;

			lock (infoBarLocker) {
				var infoBarId = infoBars.Where (k => k.Value.InfoBarElement == infoBarUIElement)
					.Select (k => k.Key)
					.FirstOrDefault ();

				if (!string.IsNullOrEmpty (infoBarId)) {
					if (infoBars.TryGetValue (infoBarId, out InfoBarWrapper infoBar)) {
						callback = infoBar?.Callback;
					}
				}
			}

			await callback ();
		}

		class InfoBarWrapper
		{
			public IVsInfoBarUIElement InfoBarElement { get; set; }
			public Func<System.Threading.Tasks.Task> Callback { get; set; }

			public uint Cookie { get; set; }
		}
	}
}
