using System;

namespace Xamarin.HotReload
{
	public static class ServiceProvider
	{
		public static T GetService<T> (this IServiceProvider provider) where T : class
			=> provider.GetService (typeof (T)) as T;
	}
}
