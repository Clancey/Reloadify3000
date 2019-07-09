using System;
using System.Reflection;

namespace Xamarin.HotReload
{
	/// <summary>
	/// A base type indicating that something in the given file has changed.
	///  May be subclassed to provide more detailed information.
	/// </summary>
	[Serializable]
	public class ReloadChange
	{
		/// <summary>
		/// The file that was changed.
		/// </summary>
		public FileIdentity File { get; }

		public ReloadChange (FileIdentity file)
		{
			File = file;
		}

		public override string ToString () => $"{GetType ().Name} in {File}";
	}
}
