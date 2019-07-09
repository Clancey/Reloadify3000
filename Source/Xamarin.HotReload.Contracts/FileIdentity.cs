using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

namespace Xamarin.HotReload
{
	[Serializable]
	public class FileIdentity : IEquatable<FileIdentity>
	{
		/// <summary>
		/// Gets the AssemblyName of the project for the file.
		/// </summary>
		public AssemblyName AssemblyName { get; }


		public string CurrentAssemblyLocation { get; set; }

		/// <summary>
		/// Gets the path of the file, relative to the project.
		/// </summary>
		public string RelativePath { get; }

		public Dictionary<string,string> Classes { get; set; }

		public string NewAssembly { get; set; }

		/// <summary>
		/// Gets the full path to the original source file
		/// </summary>
		public string SourcePath { get; set; }

		/// <summary>
		/// If it is a text file, its <see cref="System.Text.Encoding"/>, otherwise <c>null</c>.
		/// </summary>
		public Encoding Encoding { get; }

		public FileIdentity (AssemblyName assemblyName, string sourcePath, string relPath, Encoding encoding = null)
		{
			AssemblyName = assemblyName ?? throw new ArgumentNullException (nameof (assemblyName));
			RelativePath = relPath ?? throw new ArgumentNullException (nameof (relPath));
			SourcePath = sourcePath ?? throw new ArgumentNullException (nameof (sourcePath));
			Encoding = encoding;
		}

		public override string ToString () => RelativePath;

		public bool Equals (FileIdentity other)
			=> string.Equals (RelativePath, other.RelativePath, StringComparison.OrdinalIgnoreCase) // FIXME: deal with case sensitive file systems
			&& Comparers.AssemblyName.Equals (AssemblyName, other.AssemblyName);

		public override bool Equals (object obj) => Equals ((FileIdentity)obj);
		public override int GetHashCode ()
			=> RelativePath.GetHashCode () ^ Comparers.AssemblyName.GetHashCode (AssemblyName);
	}
}
