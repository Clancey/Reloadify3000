using System;
using System.Threading;
using System.Threading.Tasks;

namespace Comet.Internal.Reload {


	public interface ICommunicator
	{
		Action<object> DataReceived { get; set; }

		Task<bool> Send<T>(T obj);
	}

	public interface ICommunicatorClient : ICommunicator {
		Task<(bool success, ICommunicatorClient client)> Connect (CancellationToken cancellationToken);
		Task Disconnect ();
	}

	public interface ITcpCommunicatorClient : ICommunicatorClient {

		Task<bool> Connect (string ip, int port);
	}

	public interface ITcpCommunicatorServer : ICommunicator {
		event EventHandler ClientConnected;

		int ClientsCount { get; }

		Task<bool> StartListening (int serverPort);

		void StopListening ();
	}
}