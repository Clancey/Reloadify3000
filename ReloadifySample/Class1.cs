using System;
namespace ReloadifySample
{
	public partial class Class1
	{
		static string foo = "Hey Guys!!! this was hotreloaded";
		public static void Init()
		{
			Console.WriteLine("New init was called");  
			//This calls an internal method, and we can still hot reload it!
			Program.FooBar();
		}
		public string Foo { get; set; } 
	}
}
