using System;
namespace HotUI.Reload {

	public class IDE {

		public static IDE Shared { get; set; } = new IDE ();
		public static void Init()
		{
			IDEManager.Shared.DataRecieved = Shared.OnDataReceived;
			Shared.MonitorEditorChanges ();
			IDEManager.Shared.StartMonitoring ();
		}
		void MonitorEditorChanges()
		{

		}
		void OnDataReceived(object message)
		{

		}
	}
}
