using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.HotReload.Ide
{
	public interface IInfoBarProvider
	{
		Task CloseAsync (string infoBarId);

		Task<string> ShowAsync (string text, bool closable = true, string actionText = null, Func<Task> action = null, string infoBarId = "");
	}
}
