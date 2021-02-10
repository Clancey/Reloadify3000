using System;
using System.Threading.Tasks;
using Esp.Resources;

namespace Comet
{
	public static class Reload
	{
		public static Task<bool> Init(string ideIP = null, int idePort = Constants.DEFAULT_PORT)
		{
			Reloadify.Reload.Instance.ReplaceType = (d) => HotReloadHelper.RegisterReplacedView(d.ClassName, d.Type);
			return Reloadify.Reload.Init(ideIP, idePort);
		}
	}
}
