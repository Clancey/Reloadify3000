using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;

namespace Reloadify.Internal {
	public class Message {
		public string Type => GetType ().Name;
	}

	public class ResetMessage : Message {

	}

	public class ErrorMessage : Message {
		public ErrorMessage (string title, string exception)
		{
			Title = title;
			Exception = exception;
		}

		public string Title { get; }

		public string Exception { get; }
	}

	public class EvalRequestMessage : Message {
		public string AssemblyName { get; set; }
		public byte[] Assembly { get; set; }
		public byte[] Pdb { get; set; }

		public bool HasDebugSymbols =>
			Pdb != null && Pdb.Length > 0;

		public List<(string NameSpace, string ClassName)> Classes { get; set; }
	}

	public class EvalMessage : INotifyPropertyChanged {
		public string MessageType;
		public string Text;
		public int Line;
		public int Column;

		public EvalMessage (string messageType, string text, int line = 0, int column = 0)
		{
			MessageType = messageType;
			Text = text;
			Line = line;
			Column = column;
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}

	public class EvalResult {
		public EvalMessage [] Messages;
		public TimeSpan Duration;
		public Type ResultType;
		public Type OriginalType;
		public List<(string ClassName, Type Type)> FoundClasses { get; set; } = new List<(string ClassName, Type Type)> ();
		public bool HasResult => Result != null;
		public object Result;
		public string Xaml;

		public bool HasErrors {
			get { return Messages != null && Messages.Any (m => m.MessageType == "error"); }
		}

	}

	public class EvalResponse {
		public EvalMessage [] Messages;
		public Dictionary<string, List<string>> WatchValues;
		public TimeSpan Duration;

		public bool HasErrors {
			get { return Messages != null && Messages.Any (m => m.MessageType == "error"); }
		}
	}
}

