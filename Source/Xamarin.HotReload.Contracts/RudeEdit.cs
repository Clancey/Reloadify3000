using System;

namespace Xamarin.HotReload
{
	[Serializable]
	public struct LineInfo
	{
		public int LineStart { get; }
		public int LinePositionStart { get; }
		public int LineEnd { get; }
		public int LinePositionEnd { get; }

		public static LineInfo Empty = new LineInfo (-1, -1);

		public bool IsEmpty => LineStart < 0;

		public LineInfo (int line, int linePos)
			: this (line, linePos, -1, -1)
		{
		}

		public LineInfo (int lineStart, int linePosStart, int lineEnd, int linePosEnd)
		{
			LineStart = lineStart;
			LinePositionStart = linePosStart;
			LineEnd = (lineEnd >= lineStart)? lineEnd : lineStart;
			LinePositionEnd = linePosEnd;
		}

		public override string ToString ()
		{
			if (IsEmpty)
				return "No line info";
			if (LinePositionStart < 0)
				return (LineEnd > LineStart)? $"Lines {LineStart} to {LineEnd}" : $"Line {LineStart}";
			if (LineEnd == LineStart && LinePositionEnd == LinePositionStart)
				return $"{LineStart}:{LinePositionStart}";
			return $"{LineStart}:{LinePositionStart} to {LineEnd}:{LinePositionEnd}";
		}
	}

	[Serializable]
	public sealed class RudeEdit
	{
		public FileIdentity File { get; }

		public LineInfo LineInfo { get; private set; }

		public string Message { get; }

		/// <summary>
		/// Returns <c>true</c> if this <see cref="RudeEdit"/> blocks
		///  further reloading in the same <see cref="File"/> until it is rectified.
		/// </summary>
		public bool BlocksReloading { get; }

		// FIXME: Add exception

		public RudeEdit (FileIdentity file, LineInfo lineInfo, string message, bool blocksReloading = false)
		{
			File = file;
			LineInfo = lineInfo;
			Message = message;
			BlocksReloading = blocksReloading;
		}

		public RudeEdit WithLineInfo (LineInfo newLineInfo)
		{
			var clone = (RudeEdit)MemberwiseClone ();
			clone.LineInfo = newLineInfo;
			return clone;
		}
	}
}
