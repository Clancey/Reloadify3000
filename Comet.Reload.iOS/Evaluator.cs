using System;
using Mono.CSharp;

namespace Comet.Internal.Reload {
	public partial class Evaluator {
		partial void PlatformSettings (CompilerSettings settings)
		{
			settings.AddConditionalSymbol ("__IOS__");
		}
		partial void PlatformInit ()
		{
			object res;
			bool hasRes;
			eval.Evaluate ("using Foundation;", out res, out hasRes);
			eval.Evaluate ("using CoreGraphics;", out res, out hasRes);
			eval.Evaluate ("using UIKit;", out res, out hasRes);
		}
	}
}
