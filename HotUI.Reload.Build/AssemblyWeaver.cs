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
			var xamlatorAssembly = System.IO.Path.GetFullPath(Path.Trim());
			var xamlatorAssemblyTmp = xamlatorAssembly + ".tmp";
			Log.LogMessage(MessageImportance.Normal, $"Weaving assembly {xamlatorAssembly}");
			AssemblyDefinition assemblyDef = AssemblyDefinition.ReadAssembly(xamlatorAssembly);
			var resources = assemblyDef.MainModule.Resources;
			var selectedResource = resources.FirstOrDefault(x => x.Name == Constants.IDE_IP_RESOURCE_NAME);
			if (selectedResource != null)
			{
				var ips = NetworkUtils.DeviceIps();
				string ipsString = String.Join("-", ips);
				if (string.IsNullOrEmpty(ipsString))
				{
					ipsString = "127.0.0.1";
				}
				Log.LogMessage(MessageImportance.Normal, $"HotUI.Reload weaved with ips {String.Join("-", ips)}");
				var currentIps = Encoding.ASCII.GetBytes(String.Join("\n", NetworkUtils.DeviceIps()));
				var newResource = new EmbeddedResource(Constants.IDE_IP_RESOURCE_NAME, selectedResource.Attributes, currentIps);
				resources.Remove(selectedResource);
				resources.Add(newResource);
				assemblyDef.Write(xamlatorAssemblyTmp);
			}
			else
			{
				Log.LogError($"Resource {Constants.IDE_IP_RESOURCE_NAME} not found in assembly {xamlatorAssembly}");
			}
			assemblyDef.Dispose();

			File.Replace(xamlatorAssemblyTmp, xamlatorAssembly, xamlatorAssembly + ".backup");
			return true;
		}
	}
}
