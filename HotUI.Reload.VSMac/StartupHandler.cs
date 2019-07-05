using System;
using MonoDevelop.Components.Commands;

namespace HotUI.Reload
{
	public class StartupHandler : CommandHandler
	{
		protected override void Run()
		{
			IDE.Init ();
		}
	}
}
