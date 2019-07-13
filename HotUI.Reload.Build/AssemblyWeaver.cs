using System;
using System.IO;
using System.Linq;
using System.Text;
using HotUI.Internal.Reload;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace HotUI.Reload.Build.Tasks
{
	public class AssemblyWeaver : Task
	{
		[Required]
		public string Path
		{
			get;
			set;
		}

		public override bool Execute()
		{
			var hotUIAssembly = System.IO.Path.GetFullPath(Path.Trim());
			var hotUIAssemblyTmp = hotUIAssembly + ".tmp";
			Log.LogMessage(MessageImportance.Normal, $"Weaving assembly {hotUIAssembly}");
			AssemblyDefinition assemblyDef = AssemblyDefinition.ReadAssembly(hotUIAssembly);
			var resources = assemblyDef.MainModule.Resources;
			var selectedResource = resources.FirstOrDefault(x => x.Name == Constants.IDE_IP_RESOURCE_NAME);
			if (selectedResource != null)
			{
				var ips = NetworkUtils.DeviceIps();
				string ipsString = String.Join("\n", ips);
				if (string.IsNullOrEmpty(ipsString))
				{
					ipsString = "127.0.0.1";
				}
				Log.LogMessage(MessageImportance.Normal, $"HotUI.Reload weaved with ips `{ipsString}`");
                var currentIps = Encoding.ASCII.GetBytes(ipsString);
                var newResource = new EmbeddedResource(Constants.IDE_IP_RESOURCE_NAME, selectedResource.Attributes, currentIps);
				resources.Remove(selectedResource);
				resources.Add(newResource);
				assemblyDef.Write(hotUIAssemblyTmp);
			}
			else
			{
				Log.LogError($"Resource {Constants.IDE_IP_RESOURCE_NAME} not found in assembly {hotUIAssembly}");
			}
			assemblyDef.Dispose();

			File.Replace(hotUIAssemblyTmp, hotUIAssembly, hotUIAssembly + ".backup");
			return true;
		}
	}
}
