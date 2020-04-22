using System;
using System.Threading;
using System.Threading.Tasks;
using Comet.Internal.Reload;
using System.Linq;
using System.Collections.Generic;

namespace Esp {
	public class DiscoveryService {
		public static DiscoveryService Shared { get; set; } = new DiscoveryService ();

		public async Task<ICommunicatorClient> FindConnection (params ICommunicatorClient [] communicators)
		{
			var cancellationTokenSource = new CancellationTokenSource ();

			var groupedCommunicators = communicators.GroupBy (x => x.GetType ()).ToList ();
			var tasks = groupedCommunicators.Select (x => Task.Run (async () => {
				foreach (var c in x) {
					var r = await c.Connect (cancellationTokenSource.Token);
					if (r.Item1)
						return r;
				}
				return (false, null);
			}));

			//var tasks = communicators.Select (x => x.Connect (cancellationTokenSource.Token));
			var result = await WaitForSuccess (tasks.ToList(),(t)=> t.Result.Item1);
			cancellationTokenSource.Cancel ();
			var connection = result.Result.Item2;
			var old = communicators.Where (x => x != result.Result.Item2);
			Task.Run (() => {
				foreach (var c in old)
					c?.Disconnect ();
			});
			return connection;
		}

		async Task<Task<T>> WaitForSuccess<T>(IList<Task<T>> tasks, Func<Task<T>,bool> isSuccesfull)
		{
			var first = await Task.WhenAny (tasks);
			if (first.IsCompleted && !first.IsCanceled && !first.IsFaulted && isSuccesfull (first))
				return first;
			tasks.Remove (first);
			if (tasks.Count == 0)
				return first;
			return await WaitForSuccess (tasks, isSuccesfull);			
		}

	}
}
