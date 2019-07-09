using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.HotReload.Ide
{
	public interface IErrorListProvider
	{
		Task ClearAsync ();

		Task ShowAsync ();

		Task AddAsync (RudeEdit[] rudeEdits);
	}
}
