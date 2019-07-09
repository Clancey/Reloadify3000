using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.HotReload
{
	public interface IFileContentProvider
	{
		/// <summary>
		/// Asynchronously retrieves the given file's current content.
		/// </summary>
		Task<Stream> GetContentAsync (FileIdentity file);
	}

	public static class FileContentProvider
	{
		/// <summary>
		/// Asynchronously retrieves the given file's current content as a <see cref="String"/>.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown when the file's encoding is not set (i.e. it's not a text file)</exception>
		public static async Task<string> GetStringContentAsync (this IFileContentProvider provider, FileIdentity file)
		{
			// Using a StreamReader here, as that correctly handles the BOM (while Encoding.GetString does not)
			using (var reader = new StreamReader (await provider.GetContentAsync (file), file.Encoding ?? Encoding.UTF8))
				return await reader.ReadToEndAsync ();
		}
	}
}
