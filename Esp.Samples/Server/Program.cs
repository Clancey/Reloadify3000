using System;
using System.Threading.Tasks;
using Esp;
using Esp.Resources;

namespace Server {
	class Program {
		static TcpCommunicatorServer server;
		static async Task Main (string [] args)
		{
			Console.WriteLine ("Hello World! Server!");
			server = new TcpCommunicatorServer ();
			server.DataReceived = (o) => {
				Console.WriteLine (o);
			};
			server.ClientConnected += Server_ClientConnected;
			await server.StartListening (Constants.DEFAULT_PORT);
			await Task.Run (async () => {
				while (true) {
					var line = Console.ReadLine ();
					await server.Send (line);
					Console.WriteLine ($"Sent to: {server.ClientsCount}");
				}
			});
		}

		private static async void Server_ClientConnected (object sender, EventArgs e)
		{
			await server.Send ("Hi");

			Console.WriteLine ($"Client Connected!: {server.ClientsCount}");
		}
	}
}
