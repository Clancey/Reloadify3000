using MonoDevelop.Components.Commands;

namespace Xamarin.HotReload.VSMac
{
	public class StartupHandler : CommandHandler
	{
		protected override void Run ()
		{
			VSMacManager.Init ();
		}
	}
}
