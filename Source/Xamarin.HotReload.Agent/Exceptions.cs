using System;
using System.Linq;
using System.Collections.Generic;

namespace Xamarin.HotReload
{
	public static class Exceptions
	{
		public static TOut FirstNonNullOrDefault<TIn,TOut> (this IEnumerable<TIn> enumerable, Func<TIn,TOut> func)
			where TOut: class
		{
			foreach (var item in enumerable) {
				var result = func (item);
				if (!(result is null))
					return result;
			}
			return null;
		}

		/// <summary>
		/// Returns the given exception, if it is of the given type,
		///  or the first inner exception that is of the given type (or null).
		/// </summary>
		public static T GetException<T> (this Exception ex) where T: Exception
		{
			switch (ex) {
			case T result:
				return result;
			case AggregateException agg:
				return agg.InnerExceptions.FirstNonNullOrDefault (GetException<T>);
			}
			return ex.InnerException?.GetException<T> ();
		}

		public static Exception Combine (IEnumerable<Exception> exns)
		{
			using (var e = exns.GetEnumerator ()) {
				if (!e.MoveNext ())
					return null;

				var single = e.Current;
				if (!e.MoveNext ())
					return single;
			}
			return new AggregateException (exns).Flatten ();
		}

		// Use ToSerializable; this doesn't take inner exceptions into account
		static bool IsSerializable (this Exception ex)
			=> ex.GetType ().IsSerializable;

		public static Exception ToSerializable (this Exception ex)
		{
			if (ex is null)
				return null;

			if (!IsSerializable (ex))
				return new Exception (ex.ToString ()); // FIXME

			if (ex is AggregateException agg) {
				var flattened = agg.Flatten ();
				if (flattened.InnerExceptions.Any (inner => !inner.IsSerializable ()))
					return Combine (flattened.InnerExceptions.Select (ToSerializable));
				return flattened;
			}

			if (!(ex.InnerException is null) && !ex.InnerException.IsSerializable ())
				return new Exception (ex.ToString ()); // FIXME

			return ex;
		}
	}
}
