using System;
using Mono.CSharp;

namespace System.Maui.Internal.Reload
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
        }
    }
}
