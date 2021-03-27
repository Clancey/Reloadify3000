using System;
using System.Threading;
using System.Threading.Tasks;
using Esp;

namespace Client {
	public class FailedConnection : ICommunicatorClient {
		public FailedConnection ()
		{
		}

		Action<object> ICommunicator.DataReceived { get; set; }

		public Task Disconnect () => Task.FromResult (true);

		async Task<(bool, ICommunicatorClient)> ICommunicatorClient.Connect (CancellationToken cancellationToken)
		{
			//await Task.Delay (1000);
			return (false,this);
		}

		Task<bool> ICommunicator.Send<T> (T obj)
		{
			throw new NotImplementedException ();
		}
	}
}
