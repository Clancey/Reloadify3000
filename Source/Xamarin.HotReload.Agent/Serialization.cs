using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Xamarin.HotReload
{
	public static class Serialization
	{
		public static byte [] SerializeObject (object obj)
		{
			using (var ms = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (ms, obj);
				return ms.ToArray ();
			}
		}

		public static object DeserializeObject (byte [] data)
		{
			using (var ms = new MemoryStream (data, writable: false)) {
				var formatter = new BinaryFormatter ();
				return formatter.Deserialize (ms);
			}
		}
	}
}
