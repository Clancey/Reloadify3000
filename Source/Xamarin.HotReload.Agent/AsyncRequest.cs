using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Xamarin.HotReload
{
	public abstract class AsyncRequest
	{
		static long nextRequestId; // atomic increment only

		static readonly Dictionary<long,AsyncRequest> requests
			= new Dictionary<long,AsyncRequest> (); // lock!

		public long RequestId { get; }

		internal AsyncRequest ()
		{
			RequestId = Interlocked.Increment (ref nextRequestId);
			lock (requests)
				requests.Add (RequestId, this);
		}

		public static void TrySetException (long requestId, Exception ex)
			=> GetAndRemoveRequest (requestId).TrySetException (ex);

		public static void TrySetResult (long requestId, object result)
			=> GetAndRemoveRequest (requestId).TrySetResult (result);

		static AsyncRequest GetAndRemoveRequest (long requestId)
		{
			AsyncRequest req;
			lock (requests) {
				req = requests [requestId];
				requests.Remove (requestId);
			}
			return req;
		}

		public abstract void TrySetException (Exception ex);
		public abstract void TrySetResult (object result);
	}

	public class AsyncRequest<T> : AsyncRequest
	{
		TaskCompletionSource<T> tcs = new TaskCompletionSource<T> ();

		public Task<T> Task => tcs.Task;

		public override void TrySetException (Exception ex) => tcs.TrySetException (ex);
		public override void TrySetResult (object result) => tcs.TrySetResult ((T)result);
	}
}
