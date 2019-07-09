using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace Xamarin.HotReload
{
	static class Comparers
	{
		public static readonly IEqualityComparer<AssemblyName> AssemblyName = new AssemblyNameComparer ();

		// Compares assembly names for the purpose of finding an adequate assembly to load.
		//  E.g. ignores PublicKeyToken if Retargetable is specified. It also ignores the
		// exact assembly version and tries to use the best match we have. This way we can
		// load a binary compiled against System.Runtime.dll v4.0.0 even though we ship
		// v4.1.0.0. This helps when we load newtonsoft json v8 but someone copiled against v7.
		//
		// e.g. Loading custom controls from `Xamarin.Forms.Dynamic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null' ...
		//        - Using `Newtonsoft.Json, Version=8.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed' to satisfy dependency `Newtonsoft.Json, Version=7.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed'
		class AssemblyNameComparer : IEqualityComparer<AssemblyName>
		{
			public bool Equals (AssemblyName x, AssemblyName y)
			{
				if (x == null) return y == null;
				if (y == null) return false;

				return string.Equals (x.Name, y.Name, StringComparison.Ordinal) &&
					((x.CultureInfo == null && y.CultureInfo == null) || (x.CultureInfo != null && x.CultureInfo.Equals (y.CultureInfo))) &&
					(x.Flags.HasFlag (AssemblyNameFlags.Retargetable) || y.Flags.HasFlag (AssemblyNameFlags.Retargetable) ||
						(x.GetPublicKeyToken () == null && y.GetPublicKeyToken () == null) || x.GetPublicKeyToken ().SequenceEqual (y.GetPublicKeyToken ()));
			}

			public int GetHashCode (AssemblyName obj)
			{
				if (obj == null)
					throw new ArgumentNullException (nameof (obj));
				return obj.Name.GetHashCode () ^
					(obj.CultureInfo != null ? obj.CultureInfo.GetHashCode () : 0);
			}
		}
	}
}
