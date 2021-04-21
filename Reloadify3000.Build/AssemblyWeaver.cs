using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Esp.Resources;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Reloadify.Internal;

namespace Reloadify3000.Build.Tasks
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
			try
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
					Log.LogMessage(MessageImportance.Normal, $"Reloadify3000 weaved with ips `{ipsString}`");
					var currentIps = Encoding.ASCII.GetBytes(ipsString);
					//Already written, move along
					if ((selectedResource as EmbeddedResource)?.GetResourceData() == currentIps)
						return true;
					var newResource = new EmbeddedResource(Constants.IDE_IP_RESOURCE_NAME, selectedResource.Attributes, currentIps);
					//Clear out all old references
					resources.Where(x => x.Name == Constants.IDE_IP_RESOURCE_NAME).ToList().ForEach(x => resources.Remove(x));
					resources.Add(newResource);
					assemblyDef.Write(CometAssemblyTmp);
					assemblyDef.Dispose();
					//Time to write the file, make sure we do this single Threaded
					File.Replace(CometAssemblyTmp, CometAssembly, CometAssembly + ".backup");
				}
				else
				{
					Log.LogError($"Resource {Constants.IDE_IP_RESOURCE_NAME} not found in assembly {CometAssembly}");
				}
				return true;
			}
			catch (IOException)
			{
				//On windows they parallel builds, which can cuase them to try and read/write to the same file.
				return true;
			}
		}
	}
}
