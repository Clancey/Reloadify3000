
namespace System.Runtime.CompilerServices
{

	//This class exists for dot net core. It prevents the MethodAccessException
	[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
	public class IgnoresAccessChecksToAttribute : Attribute
	{
		public IgnoresAccessChecksToAttribute(string assemblyName)
		{
			AssemblyName = assemblyName;
		}

		public string AssemblyName { get; }
	}
}