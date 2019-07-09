using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Xamarin.HotReload.Xaml
{
	public class XamlTextRangeFinder
	{
		static readonly Regex rxXmlDecl = new Regex ("<\\?xml .*?\\?>\\r?\\n?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public XamlTextRangeFinder (string xaml)
		{
			if (!string.IsNullOrEmpty (xaml)) {
				// Remove XML Declaration since the line number we get back doesn't consider it exists
				xaml = rxXmlDecl.Replace (xaml, string.Empty);

				xml = XDocument.Parse (xaml, LoadOptions.SetLineInfo);
				allNodes.AddRange (xml.DescendantNodes ());
			}
		}

		XDocument xml;
		List<XNode> allNodes = new List<XNode> ();

		bool hasXmlDeclaration = false;

		public XamlPosition RefineTextRange (int startLine, int startLinePos)
		{
			if (allNodes.Count <= 0)
				return new XamlPosition (startLine, startLinePos, -1, -1);

			XNode match = default;

			// Get the last element in the list that matches the given start location
			foreach (var node in allNodes) {
				var nli = (IXmlLineInfo)node;

				if (nli.LineNumber <= startLine && nli.LinePosition - 1 <= startLinePos)
					match = node;
			}

			return GetEndPositionOfElement (startLine, startLinePos, match as XElement);
		}

		static XamlPosition GetEndPositionOfElement (int startLine, int startLinePos, XElement x)
		{
			var lineInfo = (IXmlLineInfo)x;

			// First check if we are in an attribute within the element and return only until the end of the attribute
			if (x.HasAttributes) {

				XAttribute attrMatch = default;

				foreach (var attr in x.Attributes ()) {
					var ali = (IXmlLineInfo)attr;

					if (ali.LineNumber == startLine && ali.LinePosition - 1 <= startLinePos)
						attrMatch = attr;
				}

				if (attrMatch != null) {
					var attrPos = (IXmlLineInfo)attrMatch;
					var attrStr = attrMatch.ToString ();
					return new XamlPosition (startLine, startLinePos, attrPos.LineNumber, attrPos.LinePosition + attrStr.Length - 1);
				}
			}

			// if we are on a node, see if there's a closing node and highlight until its end if so
			if (x.LastNode != null) {
				var i = (IXmlLineInfo)x.LastNode;
				var str = x.LastNode.ToString ().TrimEnd ('\r', '\n');
				return new XamlPosition (startLine, startLinePos, i.LineNumber, i.LinePosition - 1 + str.Length - 1);
			}

			// if we are just on a single node, highlight until its end
			var xstr = x.ToString ().TrimEnd ('\r', '\n');
			return new XamlPosition (startLine, startLinePos, lineInfo.LineNumber, lineInfo.LinePosition - 1 + xstr.Length - 1);
		}


		public struct XamlPosition
		{
			public XamlPosition (int lineStart, int linePosStart, int lineEnd, int linePosEnd)
			{
				LineStart = lineStart;
				LinePositionStart = linePosStart;
				LineEnd = lineEnd;
				LinePositionEnd = linePosEnd;

				if (LineEnd < LineStart)
					LineEnd = LineStart;
				if (LineEnd == LineStart && LinePositionEnd < LinePositionStart)
					LinePositionEnd = LinePositionStart;
			}

			public int LineStart;
			public int LinePositionStart;
			public int LineEnd;
			public int LinePositionEnd;
		}
	}
}