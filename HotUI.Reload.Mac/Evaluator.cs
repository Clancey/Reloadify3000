using System;
using Mono.CSharp;

namespace HotUI.Internal.Reload {
	public partial class Evaluator {
		partial void PlatformSettings (CompilerSettings settings)
		{
			settings.AddConditionalSymbol ("__MACOS__");
		}

		partial void PlatformInit ()
		{
			object res;
			bool hasRes;
			eval.Evaluate ("using Foundation;", out res, out hasRes);
			eval.Evaluate ("using CoreGraphics;", out res, out hasRes);
			eval.Evaluate ("using AppKit;", out res, out hasRes);
		}
	}
}
