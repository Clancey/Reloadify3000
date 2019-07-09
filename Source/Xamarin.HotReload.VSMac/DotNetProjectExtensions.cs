using System.Reflection;
using MonoDevelop.Projects;

namespace Xamarin.HotReload.VSMac
{
	public static class DotNetProjectExtensions
	{
		public static ProjectFlavor GetProjectFlavor (this DotNetProject project)
		{
			var projectTypes = project?.GetTypeTags ();

			if (projectTypes != null) {
				foreach (var projectType in projectTypes) {
					switch (projectType) {
					case "XamarinIOS":
						return ProjectFlavor.iOS;
					case "XamMac2":
						return ProjectFlavor.Mac;
					case "MonoDroid":
						return ProjectFlavor.Android;
					}
				}
			}

			return ProjectFlavor.None;
		}

		public static string GetProjectPath (this MonoDevelop.Ide.Gui.Document doc)
			=> doc.FileName.ToRelative (doc.Owner.BaseDirectory).ToString ();

		public static AssemblyName GetProjectAssemblyName (this MonoDevelop.Ide.Gui.Document doc)
		{
			var proj = doc.Owner as Project;
			var projAssemblyName = proj?.ProjectProperties.GetValue ("AssemblyName", string.Empty);

			AssemblyName assemblyName = null;
			if (!string.IsNullOrEmpty (projAssemblyName))
				assemblyName = new AssemblyName (projAssemblyName);

			return assemblyName;
		}
	}
}
