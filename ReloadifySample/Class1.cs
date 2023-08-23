using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ReloadifySample
{
	public partial class Class1
	{
		string test; 
		string bar = "barValue";    
		string bar2 = "barValue";   
		string foo;
		string bar3 = "barValue";
		string bar4 = "barValue"; 
		string bar5 = "barValue"; 
		string bar6 = "barValue"; 
		string foo22 = "gfgfd";
		string bar7 = "barValue";
		public string Bar2{
			get => bar3;  
			set => bar3 = value; 
		}
		public static void Init()
		{
			Console.WriteLine("New init was called!!");    
			//This calls an internal method, and we can still hot reload it! 
			//Program.FooBar();
			var c = new Class1
			{
				//Test7 = "New Class Test 7"
			};
			Program.C.Foo = "Test6!!";
			Console.WriteLine(Program.C.ToString());
			Console.WriteLine($"Compare: {c.Foo} : {Program.C.Foo}");
		}
		public string Foo  
		{
			get => foo;
			set => foo = value;
		} 
		public override string ToString() => $"To String!!!:{foo22}"; 

	}
	
}

