using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
//using Microsoft.Language.Xml;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;

namespace Xamarin.HotReload.Ide.Editor
{
	class XamlUnsupportedEditTagger : SimpleTagger<IErrorTag>, IDisposable
	{
		IdeManager ide;
		ITextBuffer buffer;
		List<TrackingTagSpan<IErrorTag>> trackingSpans;

		public XamlUnsupportedEditTagger (IdeManager ide, ITextBuffer buffer) : base (buffer)
		{
			this.ide = ide;
			this.buffer = buffer;
			this.trackingSpans = new List<TrackingTagSpan<IErrorTag>> ();

			ide.AgentReloadResultReceived += IdeManager_AgentReloadResultReceived;
			ide.AgentStatusChanged += IdeManager_AgentStatusChanged;
			ide.Settings.SettingsChanged += IdeManager_SettingsChanged;

			var filename = buffer?.GetFileName ();

			if (ide.RudeEdits.TryGetValue(filename, out var rudeEdits))
				ReloadRudeEdits (rudeEdits);
		}

		void IdeManager_SettingsChanged (object sender, EventArgs e)
		{
			if (!ide.Settings.HotReloadEnabled)
				ClearAllTags ();
		}

		void IdeManager_AgentStatusChanged (object sender, AgentStatusMessage e)
		{
			// Clear out the tags if we are no longer debugging
			if (e.State == HotReloadState.Disabled || e.State == HotReloadState.Failed || e.State == HotReloadState.Starting)
				ClearAllTags ();
		}

		void ClearAllTags()
		{
			var sw = Stopwatch.StartNew ();

			foreach (var s in trackingSpans)
				RemoveTagSpan (s);
			trackingSpans.Clear ();

			ide.Logger.Log (LogLevel.Perf, $"Cleared Rude Edit Tags in {sw.ElapsedMilliseconds}ms");
		}

		void IdeManager_AgentReloadResultReceived (object sender, ReloadTransactionMessage e)
		{
			if (!ide.Settings.HotReloadEnabled)
				return;

			var filename = buffer?.GetFileName ();

			if (string.IsNullOrEmpty (filename))
				return;

			// Only reload when we get changes for the filename of our buffer
			if (!e.Transactions.Any (txn => filename.Equals (txn.Change.File.SourcePath)))
				return;

			// Clear previous tags
			ClearAllTags ();

			// Load the new rude edits
			if (ide.RudeEdits.TryGetValue (filename, out var rudeEdits))
				ReloadRudeEdits (rudeEdits);
		}

		void ReloadRudeEdits(RudeEdit[] rudeEdits)
		{
			if (rudeEdits == null || !rudeEdits.Any ())
				return;

			var fullXaml = string.Empty;

			try {
				fullXaml = buffer.CurrentSnapshot.GetFullSpan ().GetText ();
			} catch (Exception ex) {
				ide.Logger.Log (LogLevel.Debug, $"Failed to get full text for XAML: {ex}");
			}

			//XmlDocumentSyntax xmlAst = default;

			//try {
			//	xmlAst = Parser.ParseText (fullXaml);
			//} catch (Exception ex) {
			//	ide.Logger.Log (Debug, $"Failed to parse XAML for with Microsoft.Language.Xml.Parser: {ex}");
			//}

			Xaml.XamlTextRangeFinder xamlRangeFinder = default;

			var sw = Stopwatch.StartNew ();

			try {
				xamlRangeFinder = new Xaml.XamlTextRangeFinder (fullXaml);
				ide.Logger.Log (LogLevel.Perf, $"Parsed XAML in {sw.ElapsedMilliseconds}ms");
			} catch (Exception ex) {
				ide.Logger.Log (LogLevel.Debug, $"Failed to parse XAML for range finder: {ex}");
			}

			sw.Restart ();

			foreach (var re in rudeEdits) {

				var sw2 = Stopwatch.StartNew ();

				// -1 off all the line and columns because those are not 0 based
				var ue = re.LineInfo;
				if (ue.IsEmpty)
					continue;

				var startLine = ue.LineStart - 1;
				var startCol = ue.LinePositionStart - 1;
				var endLine = ue.LineEnd - 1;
				var endCol = ue.LinePositionEnd - 1;

				ITextSnapshotLine line = default;

				// Don't even bother if we have no document line position start
				if (startLine < 0)
					continue;

				// Beginning of line if we have no position start
				// Try to find the first non whitespace character too
				if (startCol < 0) {
					line = buffer?.CurrentSnapshot?.GetLineFromLineNumber (startLine);

					var lineText = line?.GetText () ?? string.Empty;

					var firstNonWhitespaceCol = 0;

					for (int i = 0; i < lineText.Length; i++) {
						if (!char.IsWhiteSpace (lineText[i])) {
							firstNonWhitespaceCol = i;
							break;
						}
					}

					startCol = firstNonWhitespaceCol;
				}
				if (startCol < 0) // Recheck for a valid value
					startCol = 0;

				//// If we weren't given an end line/col for the range, try refining it with the xaml by parsing it
				//if (endLine < 0 || endCol < 0 && xmlAst != null) {
				//	foreach (var c in xmlAst.ChildNodes) {
				//		var startBufferPos = buffer.CurrentSnapshot.GetPosition (startLine, startCol);
				//		var locNode = SyntaxLocator.FindNode (c, startBufferPos, n => true);

				//		if (locNode != null) {
				//			buffer.CurrentSnapshot.GetLineAndCharacter (locNode.End, out endLine, out endCol);
				//			break;
				//		}
				//	}
				//}

				// If we didn't use Microsoft.Language.Xml.Parser to find it, try our backup range finder code
				if (endLine < 0 || endCol < 0 && xamlRangeFinder != null) {
					try {
						var refinedRange = xamlRangeFinder.RefineTextRange (startLine, startCol);
						endLine = refinedRange.LineEnd;
						endCol = refinedRange.LinePositionEnd;
					} catch (Exception ex) {
						ide.Logger.Log (LogLevel.Debug, $"Failed to find refined text range: {ex}");
					}
				}

				// If no end line, use the same as the start
				if (endLine < 0 || endLine < startLine)
					endLine = startLine;

				if (endCol < 0 || endCol < startCol) {
					if (line == null)
						line = buffer?.CurrentSnapshot?.GetLineFromLineNumber (startLine);

					endCol = line?.Length ?? -1;
					if ((line?.LineBreakLength ?? 0) <= 0)
						endCol--;
				}
				if (endCol < 0 || endCol < startCol) // Recheck for a valid value
					endCol = startCol;

				// Get the span for our given range
				var span = buffer.CurrentSnapshot.GetSpan (startLine, startCol, endLine, endCol);

				// Get a tracking span for the given unsupported edit
				var trackingSpan = buffer.CurrentSnapshot.CreateTrackingSpan (span, SpanTrackingMode.EdgeExclusive);

				// Error tag to show for the span
				var errorTag = new ErrorTag (PredefinedErrorTypeNames.SyntaxError, re.Message ?? "Unsupported Hot Reload Xaml Edit");

				// Create the tag with our underlying simple tagger
				var tagSpan = CreateTagSpan (trackingSpan, errorTag);

				// Track the tag to be able to remove it
				trackingSpans.Add (tagSpan);

				ide.Logger.Log (LogLevel.Perf, $"Added Rude Edit {ue.LineStart}:{ue.LinePositionStart} in {sw2.ElapsedMilliseconds}ms");
			}

			ide.Logger.Log (LogLevel.Perf, $"Total Rude Edit time elapsed: {sw.ElapsedMilliseconds}ms");
		}

		public void Dispose()
		{
			trackingSpans?.Clear ();
			trackingSpans = null;

			buffer = null;

			if (ide != null) {
				ide.AgentStatusChanged -= IdeManager_AgentStatusChanged;
				ide.AgentReloadResultReceived -= IdeManager_AgentReloadResultReceived;
				ide.Settings.SettingsChanged -= IdeManager_SettingsChanged;
			}
		}
	}
}