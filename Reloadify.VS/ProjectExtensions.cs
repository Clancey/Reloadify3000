//
//
// Author:
//   Sandy Armstrong <sandy@xamarin.com>
//
// Copyright 2015 Xamarin Inc. All rights reserved.
// Copyright 2016 Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using EnvDTE;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.Build.Evaluation;
using Project = EnvDTE.Project;
using System.IO;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Reloadify;
using Microsoft.CodeAnalysis.CSharp;

namespace Xamarin.HotReload.Vsix
{
	static class ProjectExtensions
	{
		public static T GetPropertyValue<T> (this Project project, int propertyId) where T : class
		{
			var hierarchy = project.ToHierarchy ();

			object propertyVal;
			if ((hierarchy.GetProperty (0, propertyId, out propertyVal) == VSConstants.S_OK))
				return propertyVal as T;

			return null;
		}

		public static IVsHierarchy ToHierarchy (this Project project)
		{
			if (project == null) throw new ArgumentNullException ("project");

			try {
				IVsHierarchy hierarchy;
				var solutionService = Package.GetGlobalService (typeof (SVsSolution)) as IVsSolution2;
				if (solutionService.GetProjectOfUniqueName (project.UniqueName, out hierarchy) == VSConstants.S_OK)
					return hierarchy;
			} catch (NotImplementedException) {
				// ignore - apparently some Project implementations in Visual Studio do not implement the FileName property
				// and we'll get a NotImplemented exception thrown here.
				return null;
			}

			return null;
		}


		//public static ProjectFlavor GetProjectFlavor (this Project adaptableProj)
		//{
		//	var projectTypeGuids = adaptableProj
		//		.AsMsBuildProject ()
		//		?.Properties
		//		?.FirstOrDefault (p => p.Name == "ProjectTypeGuids")
		//		?.EvaluatedValue;

		//	if (!string.IsNullOrEmpty (projectTypeGuids)) {
		//		projectTypeGuids = projectTypeGuids.ToUpperInvariant ();

		//		if (projectTypeGuids.Contains (Constants.MonodroidProjectGuidString.ToUpperInvariant ()))
		//			return ProjectFlavor.Android;
		//		else if (projectTypeGuids.Contains (Constants.MonoTouchUnifiedProjectGuidString.ToUpperInvariant ()))
		//			return ProjectFlavor.iOS;
		//		//else if (projectTypeGuids.Contains(Constants.WpfProjectGuidsString.ToUpperInvariant()))
		//		//    return ProjectFlavor.Wpf;
		//	}

		//	return ProjectFlavor.None;
		//}

		public static bool GetBoolPropertyValue (
			this Microsoft.Build.Evaluation.Project msbuildProj,
			string property,
			bool defaultValue)
		{
			var strVal = msbuildProj.GetPropertyValue (property);

			bool val;
			return bool.TryParse (strVal, out val) ? val : defaultValue;
		}

		public static IEnumerable<Project> GetProjects (this Solution solution)
			=> solution.Projects.Cast<Project> ().SelectMany (GetProjects);

		public static IEnumerable<Project> GetProjects (this Project project)
		{
			if (project.Kind != EnvDTE.Constants.vsProjectKindSolutionItems) {
				yield return project;
				yield break;
			}

			// project is a solution folder; look inside it for other projects and solution folders
			for (var i = 1; i <= project.ProjectItems.Count; i++) {
				var subProject = project.ProjectItems.Item (i).SubProject;
				if (subProject == null)
					continue;

				foreach (var proj in subProject.GetProjects ())
					yield return proj;
			}
		}

		public static Microsoft.Build.Evaluation.Project AsMsBuildProject (this Project project)
		{
			try {
				if (string.IsNullOrEmpty (project.FullName)) return null;

				return
				ProjectCollection.GlobalProjectCollection.GetLoadedProjects (project.FullName).FirstOrDefault ()
				?? ProjectCollection.GlobalProjectCollection.LoadProject (project.FullName);
			} catch (Exception) {
				return null;
			}
		}


		public static string GetRelativePathTo (this FileSystemInfo from, FileSystemInfo to)
		{
			Func<FileSystemInfo, string> getPath = fsi => {
				var d = fsi as DirectoryInfo;
				return d == null ? fsi.FullName : d.FullName.TrimEnd (Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
			};

			var fromPath = getPath (from);
			var toPath = getPath (to);

			var fromUri = new Uri (fromPath);
			var toUri = new Uri (toPath);

			var relativeUri = fromUri.MakeRelativeUri (toUri);
			var relativePath = Uri.UnescapeDataString (relativeUri.ToString ());

			return relativePath.Replace ('/', Path.DirectorySeparatorChar);
		}
	}

	internal static class VSHelpers
	{


		public static async System.Threading.Tasks.Task SearchForPartialClasses(string filePath,string fileContents, string solutionPath)
		{
			var workspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();

			if (workspace?.CurrentSolution?.FilePath != solutionPath)
				await workspace.OpenSolutionAsync(solutionPath);



			var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(fileContents);

			var root = tree.GetCompilationUnitRoot();
			var collector = new ClassCollector();
			collector.Visit(root);
			var classes = collector.Classes.Select(x => x.GetClassNameWithNamespace()).ToList();
			//collector.Classes.Where(x=> x.)


			var docs = workspace.CurrentSolution.Projects?.SelectMany(x => x.Documents.Where(y => y.FilePath == filePath));
			var doc = docs.FirstOrDefault();
			var model = await doc.GetSemanticModelAsync();
			var compilation = model.Compilation;
			//var symbol = compilation.GetTypeByMetadataName(fullName);
			//var refrences = symbol.DeclaringSyntaxReferences;

			//Console.WriteLine(refrences);



			//foreach(var p in workspace.CurrentSolution.Projects)
			//{
			//	//new Microsoft.CodeAnalysis.Document().WithFilePath("");
			//	Console.WriteLine(p.FilePath);
			//	foreach(var d in p.Documents)
			//	{
			//		d.id
			//	}

		}

		internal static IVsTextView GetIVsTextView (string filename)
		{
			IVsTextView result = default;

			var dte = (EnvDTE80.DTE2)Package.GetGlobalService (typeof (SDTE));
			var isp = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)dte;
			var sp = new Microsoft.VisualStudio.Shell.ServiceProvider (isp);

			if (VsShellUtilities.IsDocumentOpen (sp, filename, Guid.Empty, out var uiHierarchy, out var itemId, out var windowFrame))
				result = VsShellUtilities.GetTextView (windowFrame);

			return result;
		}

		//internal static IWpfTextViewHost GetWpfTextViewHost (IVsTextView textView)
		//{
		//	var dte = (EnvDTE80.DTE2)Package.GetGlobalService (typeof (SDTE));
		//	var isp = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)dte;
		//	var sp = new Microsoft.VisualStudio.Shell.ServiceProvider (isp);

		//	var textManager = (IVsTextManager)ServiceProvider.GetService<SVsTextManager> (sp);
		//	var componentModel = (IComponentModel)ServiceProvider.GetService<SComponentModel> (sp);
		//	var editor = componentModel.GetService<IVsEditorAdaptersFactoryService> ();

		//	return editor.GetWpfTextViewHost(textView);
		//}

		internal static ITextDocument GetDocument (IWpfTextViewHost viewHost)
		{
			viewHost.TextView.TextDataModel.DocumentBuffer.Properties.TryGetProperty (typeof (ITextDocument), out ITextDocument document);
			return document;
		}

		//internal static Encoding GetEncoding(string filename)
		//{
		//	try {
		//		var tv = GetIVsTextView (filename);
		//		var wpfTv = GetWpfTextViewHost (tv);
		//		var doc = GetDocument (wpfTv);

		//		return doc.Encoding;
		//	} catch (Exception ex) {
		//		//logger?.Log (ex, LogLevel.Warn);
		//		return null;
		//	}
		//}
	}
}