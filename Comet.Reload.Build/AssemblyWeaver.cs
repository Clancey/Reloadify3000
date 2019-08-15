using System;
using System.IO;
using System.Linq;
using System.Text;
using Comet.Internal.Reload;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace Comet.Reload.Build.Tasks
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
			var CometAssembly = System.IO.Path.GetFullPath(Path.Trim());
			var CometAssemblyTmp = CometAssembly + ".tmp";
			Log.LogMessage(MessageImportance.Normal, $"Weaving assembly {CometAssembly}");
			AssemblyDefinition assemblyDef = AssemblyDefinition.ReadAssembly(CometAssembly);
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
				Log.LogMessage(MessageImportance.Normal, $"Comet.Reload weaved with ips `{ipsString}`");
                var currentIps = Encoding.ASCII.GetBytes(ipsString);
                var newResource = new EmbeddedResource(Constants.IDE_IP_RESOURCE_NAME, selectedResource.Attributes, currentIps);
				resources.Remove(selectedResource);
				resources.Add(newResource);
				assemblyDef.Write(CometAssemblyTmp);
			}
			else
			{
				Log.LogError($"Resource {Constants.IDE_IP_RESOURCE_NAME} not found in assembly {CometAssembly}");
			}
			assemblyDef.Dispose();

			File.Replace(CometAssemblyTmp, CometAssembly, CometAssembly + ".backup");
			return true;
		}
	}
}
