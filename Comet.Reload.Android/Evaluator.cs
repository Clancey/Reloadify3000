using System;
using Mono.CSharp;

namespace Comet.Internal.Reload
{
    public partial class Evaluator
    {
        partial void PlatformSettings(CompilerSettings settings)
        {
            settings.AddConditionalSymbol("__ANDROID__");
        }
        partial void PlatformInit()
        {
            object res;
            bool hasRes;
            eval.Evaluate("using Android.OS;", out res, out hasRes);
            eval.Evaluate("using Android.App;", out res, out hasRes);
            eval.Evaluate("using Android.Widget;", out res, out hasRes);
        }
    }
}
