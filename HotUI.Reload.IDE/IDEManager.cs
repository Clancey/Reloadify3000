using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotUI.Internal.Reload;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HotUI.Reload {
	public class IDEManager {

		public static IDEManager Shared { get; set; } = new IDEManager ();

		ITcpCommunicatorServer server;
		IDEManager()
		{
			server = new TcpCommunicatorServer ();
			server.DataReceived = (o) => DataRecieved?.Invoke (o);
		}
		//TODO: change to fixed size dictionary
		Dictionary<string, string> currentFiles = new Dictionary<string, string> ();
		public async void HandleDocumentChanged (DocumentChangedEventArgs e)
		{
			if (server.ClientsCount == 0)
				return;

			if (string.IsNullOrWhiteSpace (e.Filename))
				return;
			if (string.IsNullOrWhiteSpace (e.Text)) {
				var code = File.ReadAllText (e.Filename);
				if (string.IsNullOrWhiteSpace (code))
					return;
				e.Text = code;
			}
			if(currentFiles.TryGetValue(e.Filename, out var oldFile) && oldFile == e.Text) {
				return;
			}
			currentFiles [e.Filename] = e.Text;
			SyntaxTree tree = CSharpSyntaxTree.ParseText (e.Text);

			var root = tree.GetCompilationUnitRoot ();
			var collector = new ClassCollector ();
			collector.Visit (root);
			var classes = collector.Classes.Select (x => x.GetClassNameWithNamespace ()).ToList();
			await server.Send (new EvalRequestMessage { Classes = classes, Code = e.Text, FileName = e.Filename });
		}

		public Action<object> DataRecieved { get; set; }

		public void StartMonitoring ()
		{
			StartMonitoring (Constants.DEFAULT_PORT);
		}

		internal void StartMonitoring (int port)
		{
			server.StartListening (port);
		}
	}
}
