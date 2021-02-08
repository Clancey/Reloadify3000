using System;
namespace ReloadifySample
{
	public partial class Class1
	{
		public static void Init()
		{ 
			//This calls an internal method, and we can still hot reload it!
			Program.FooBar();
		}
		public string Foo { get; set; }
	}
}
