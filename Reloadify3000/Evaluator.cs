//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Reflection;
//using System.Text;

//namespace Reloadify.Internal {
//	public partial class Evaluator {
//		Assembly CometAssembly;
//		Type _viewType;
//		Type _handlerType;
//		Type ViewType => _viewType ?? (_viewType = CometAssembly?.GetType ("Comet.View"));
//		Type HandlerType => _handlerType ?? (_handlerType = CometAssembly?.GetType ("Comet.IViewHandler"));
//		Dictionary<string, string> replacedClasses = new Dictionary<string, string> ();


//		static string ToFullName ((string NameSpace, string ClassName) data) => ToFullName (data.NameSpace, data.ClassName);
//		static string ToFullName (string NameSpace, string ClassName)
//		{
//			var name = string.IsNullOrWhiteSpace (NameSpace) ? "" : $"{NameSpace}.";
//			return $"{name}{ClassName}";
//		}

//		static string Replace (string code, Dictionary<string, string> replaced)
//		{

//			string newCode = code;
//			foreach (var pair in replaced) {
//				if (pair.Key == pair.Value)
//					continue;
//				newCode = ReplaceAllIndexesOf (newCode, pair.Key, pair.Value);
//			}

//			//Debug.WriteLine("Finished Replace");
//			return newCode;
//		}
//		static List<char> allowedCharacters = new List<char> {
//				' ',
//				'\r',
//				'<',
//				'>',
//				'.',
//				':',
//				';',
//				(char)10,
//				(char)32,
//				(char)40 //Return
//           };
//		public static string ReplaceAllIndexesOf (string str, string value, string replace)
//		{
//			var stringBuilder = new StringBuilder ();
//			bool foundMAtches = false;
//			var codeLength = str.Length;
//			if (String.IsNullOrEmpty (value))
//				throw new ArgumentException ("the string to find may not be empty", "value");
//			int previousIndex = 0;
//			for (int i = 0; ; i += value.Length) {
//				// Debug.WriteLine($"Looking for {value} at {i}");
//				i = str.IndexOf (value, i);
//				if (i == -1) {
//					if (foundMAtches) {
//						stringBuilder.Append (str.Substring (previousIndex, codeLength - previousIndex));
//						return stringBuilder.ToString ();
//					}
//					return str;
//				}

//				//Debug.WriteLine($"Found {value} at {i}");
//				foundMAtches = true;
//				var length = value.Length;
//				var prev = i > 0 ? str [i - 1] : ' ';
//				var next = i + length < codeLength ? str [i + length] : ' ';
//				var nextIndex = i + length;

//				var prevAllowed = allowedCharacters.Contains (prev);
//				var nextAllowed = allowedCharacters.Contains (next);
//				// Debug.WriteLine($"PrevAllowed: `{(int)prev}` {prevAllowed}");
//				//Debug.WriteLine($"NextAllowed:`{(int)next}` {nextAllowed}");
//				if (prevAllowed && nextAllowed) {

//					//Debug.WriteLine($"Both characters are allowed. Appending Current and {replace}");
//					//Replace
//					stringBuilder.Append (str.Substring (previousIndex, i - previousIndex));
//					stringBuilder.Append (replace);
//					//Debug.WriteLine(stringBuilder.ToString());
//				} else {
//					//Debug.WriteLine($"Invalid characters Appending {previousIndex}-{nextIndex - previousIndex}  : {str.Substring(previousIndex, nextIndex - previousIndex)}");
//					stringBuilder.Append (str.Substring (previousIndex, nextIndex - previousIndex));

//					// Debug.WriteLine(stringBuilder.ToString());
//				}
//				previousIndex = nextIndex;
//				i = nextIndex;


//			}
//			return stringBuilder.ToString ();
//		}
//	}
//}
