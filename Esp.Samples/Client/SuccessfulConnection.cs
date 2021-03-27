using System;
using System.Threading;
using System.Threading.Tasks;
using Esp;

namespace Client {
	public class SuccessfulConnection: ICommunicatorClient {
		public SuccessfulConnection ()
		{
		}

		Action<object> ICommunicator.DataReceived { get; set; }

		public Task Disconnect () => Task.FromResult (true);

		async Task<(bool, ICommunicatorClient)> ICommunicatorClient.Connect (CancellationToken cancellationToken)
		{
			await Task.Delay (2000);
			return (true,this);
		}

		Task<bool> ICommunicator.Send<T> (T obj)
		{
			throw new NotImplementedException ();
		}
	}
}
