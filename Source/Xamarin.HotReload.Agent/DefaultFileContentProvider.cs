using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Xamarin.HotReload
{
	public class DefaultFileContentProvider : IFileContentProvider
	{
		public async Task<Stream> GetContentAsync (FileIdentity file)
		{
			var req = new AsyncRequest<byte []> ();
			HotReloadAgent.SendToIde (new FileContentRequest { RequestId = req.RequestId, File = file });
			return new MemoryStream (await req.Task, writable: false);
		}
	}
}
