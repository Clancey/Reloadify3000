using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Reloadify {
	public class ClassCollector : CSharpSyntaxWalker
	{
		public ICollection<UsingDirectiveSyntax> Usings { get; } = new List<UsingDirectiveSyntax> ();
		public ICollection<ClassDeclarationSyntax> Classes { get; } = new List<ClassDeclarationSyntax> ();
		public ICollection<ClassDeclarationSyntax> PartialClasses { get; } = new List<ClassDeclarationSyntax>();

		public override void VisitUsingDirective (UsingDirectiveSyntax node)
		{
			base.VisitUsingDirective (node);
			Usings.Add (node);
		}
		public override void VisitClassDeclaration (ClassDeclarationSyntax node)
		{
			base.VisitClassDeclaration (node);
			//If its a nested class we don't care
			if (node.Parent is ClassDeclarationSyntax)
				return;
			//Lets check for old stuff
			 Classes.Add(node);
			var c = node.GetClassNameWithNamespace();
		
			if (node.Modifiers.Any(x => (string)x.Value == "partial"))
				PartialClasses.Add(node);
		}

	}

}
