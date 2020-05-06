using System;
namespace Reloadify {

	public class IDE {

		public static IDE Shared { get; set; } = new IDE ();
		public static void Init()
		{
			IDEManager.Shared.DataRecieved = Shared.OnDataReceived;
		}
		void DebuggingStarted()
		{
            IDEManager.Shared.StartMonitoring();
        }
        void DebuggingStopped()
        {

            IDEManager.Shared.StartMonitoring();
        }
        void OnDataReceived(object message)
        {
            Console.WriteLine("Data recieved");
        }
	}
}
