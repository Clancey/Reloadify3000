using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace Xamarin.HotReload.Ide.Editor
{
	[Export (typeof (ITaggerProvider))]
	[TagType (typeof (IErrorTag))]
	[ContentType ("Roslyn Languages")]
	[ContentType ("XAML")]
	class XamlUnSupportedEditTaggerProvider : ITaggerProvider
	{
		IdeManager ide;

		[ImportingConstructor]
		public XamlUnSupportedEditTaggerProvider (IdeManager ide)
		{
			this.ide = ide;
		}

		public ITagger<T> CreateTagger<T> (ITextBuffer buffer) where T : ITag
		{
			if (!typeof (IErrorTag).IsAssignableFrom (typeof (T)))
				return null;

			return buffer.Properties.GetOrCreateSingletonProperty (() => new XamlUnsupportedEditTagger (ide, buffer)) as ITagger<T>;
		}
	}
}