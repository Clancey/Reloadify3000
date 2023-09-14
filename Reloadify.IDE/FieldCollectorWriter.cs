﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Reloadify
{
	public static class ClassCodeRewriter
	{
		public static SyntaxTree FixCode(CompilationUnitSyntax root, CSharpParseOptions parseOptions, string filePath, Dictionary<(string Namespace, string ClassName), Dictionary<string, ITypeSymbol>> ExistingFields)
		{
			var syntaxWriter = new FieldCollectorWriter { ExistingFields = ExistingFields };
			var newRoot = syntaxWriter.Visit(root);
			newRoot = new ClassFieldCollectorWriter { FoundFields = syntaxWriter.FoundFields }.Visit(newRoot);
			var newCode = newRoot.ToFullString();
			return CSharpSyntaxTree.ParseText(newCode, parseOptions, encoding: System.Text.Encoding.Default);
		}
	}
	public class FieldCollectorWriter : CSharpSyntaxRewriter
	{
		public List<(string Name, string Type, string Value)> FoundFields { get; set; } = new();
		public Dictionary<(string Namespace, string ClassName), Dictionary<string, ITypeSymbol>> ExistingFields = new();
		public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
		{
			var firstVar = node.Declaration.Variables[0];
			var name = firstVar.Identifier.ToString();
			var type = node.Declaration.Type.ToString();
			var fullName = (node.Parent as ClassDeclarationSyntax).GetClassNameWithNamespace();
			//We need to comment out, and change all new fields, we ignore existing fields and statics
			if (node.Modifiers.Any(x => (string)x.Value == "static"))
				return base.VisitFieldDeclaration(node);
			if (ExistingFields.TryGetValue(fullName, out var oldFields))
			{
				if (oldFields.TryGetValue(name, out var oldType) && oldType.ToString() == type)
				{
					return base.VisitFieldDeclaration(node);

				}
			}

			//Ok, this is an new field. or a new return type. Lets fix it!
			var leading = node.GetLeadingTrivia();
			var trailing = node.GetTrailingTrivia();
			FoundFields.Add((name, type, firstVar.Initializer?.Value.ToFullString()));

			return node.WithLeadingTrivia(leading.Add(SyntaxFactory.Comment("/*"))).WithTrailingTrivia(trailing.Insert(0, SyntaxFactory.Comment(" */")));
		}

	}
	public class ClassFieldCollectorWriter : CSharpSyntaxRewriter
	{
		public List<(string Name, string Type, string Value)> FoundFields { get; set; } = new();
		static string GetGetValue(string name, string type) => $"\t\tReloadify.DictionaryHelper.GetValue<{type}>(this, \"{name}\", __ReloadifyNewFields__, __ReloadifyNewFieldsDefaultValues__)\r\n";
		static string GetSetValue(string name, string type) => $"\t\tReloadify.DictionaryHelper.SetValue(this, \"{name}\", value, __ReloadifyNewFields__, __ReloadifyNewFieldsDefaultValues__)\r\n";
		const string newFieldsProperty = "\t\tstatic Dictionary<object, Dictionary<string, object>> __ReloadifyNewFields__ = new Dictionary<object, Dictionary<string, object>>();\r\n";
		static string newFieldsDefaultProperty (string values) => $"\t\tstatic Dictionary<string, object> __ReloadifyNewFieldsDefaultValues__ = new Dictionary<string, object>(){{ {values} }};\r\n";

		public Dictionary<(string Namespace, string ClassName), Dictionary<string, ITypeSymbol>> ExistingFields = new();

		public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
		{
			if(!FoundFields.Any())
				return base.VisitClassDeclaration(node);
			List<MemberDeclarationSyntax> members = new();
			List<string> defaultValueStrings = new();
			foreach (var field in FoundFields)
			{
				var getValue = GetGetValue(field.Name, field.Type);
				var setValue = GetSetValue(field.Name, field.Type);
				var member = SyntaxFactory.ParseMemberDeclaration($"{field.Type} {field.Name} {{ get => {getValue}; set => {setValue};}}");
				members.Add(member);
				var value =  string.IsNullOrEmpty(field.Value) ? "default" : field.Value;
				defaultValueStrings.Add($"[\"{field.Name}\"] = {value}");
			}
			members.Add(SyntaxFactory.ParseMemberDeclaration(newFieldsProperty));
			var defaultDicationry = newFieldsDefaultProperty(string.Join(",",defaultValueStrings));
			members.Add(SyntaxFactory.ParseMemberDeclaration(defaultDicationry));
			return node.AddMembers(members.ToArray());
		}
	}
}