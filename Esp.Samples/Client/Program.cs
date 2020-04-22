using System;
using System.Threading.Tasks;
using Comet.Internal.Reload;
using Esp;

namespace Client {
	class Program {
		static ICommunicatorClient connection;
		static async Task Main (string [] args)
		{
			Console.WriteLine ("Hello World: Client!");
			await Task.Delay (1000);
			var tcpClients = TcpCommunicatorClient.GetTcpCommunicatorsFromResource ();
			connection = await DiscoveryService.Shared.FindConnection (tcpClients.ToArray());
			if(connection == null) {
				Console.WriteLine ("No connection found!!!!");
				return;
			}

			connection.DataReceived = (m) => {
				Console.WriteLine (m);
			};
			Console.WriteLine ($"Found a connection: {connection.GetType ()}");
			await Task.Delay (1000);
			var sendSuccess = await connection.Send ("Hello");
			Console.WriteLine ($"Sent message!");
			Console.ReadLine ();
		}
	}
}
