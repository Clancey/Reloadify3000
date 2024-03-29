﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Esp.Resources;
using Reloadify.Internal;
using Esp;

namespace Reloadify {
	public class IDEManager {
		public bool IsEnabled { get; set; } = true;
		public static IDEManager Shared { get; set; } = new IDEManager ();
		public Action<IEnumerable<Diagnostic>> OnErrors { get; set; }

		public Action<string> LogAction { get; set; }

        TcpCommunicatorServer server;
		IDEManager ()
		{
			server = new TcpCommunicatorServer ();
			server.ClientConnected += Server_ClientConnected;
			server.DataReceived = (o) => DataRecieved?.Invoke (o);
		}

		private async void Server_ClientConnected(object sender, ClientConnectedEventArgs e)
		{
			Log("Client Connected");
			await Task.Delay(1000);
			foreach(var message in currentMessages)
            {
				await server.SendToClient(e.ClientId, message);
				await Task.Delay(100);
            }
		}

		string textChangedFile;
		System.Timers.Timer textChangedTimer;
		public void TextChanged (string file)
		{
			if (server.ClientsCount == 0)
				return;
			textChangedFile = file;
			if (textChangedTimer == null) {
				textChangedTimer = new System.Timers.Timer (600);
				textChangedTimer.Elapsed += async (s, e) => {
					var code = await GetActiveDocumentText.Invoke (textChangedFile);
					HandleDocumentChanged (new DocumentChangedEventArgs (textChangedFile, code));
				};
			} else
				textChangedTimer.Stop ();
			textChangedTimer.Start ();
		}

		public Func<string, Task<string>> GetActiveDocumentText { get; set; }
		FixedSizeDictionary<string, string> currentFiles = new FixedSizeDictionary<string, string> (10);
		List<EvalRequestMessage> currentMessages = new List<EvalRequestMessage>();
		public async void HandleDocumentChanged (DocumentChangedEventArgs e)
		{
			textChangedTimer?.Stop ();
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
			if (currentFiles.TryGetValue (e.Filename, out var oldFile) && oldFile == e.Text) {
				return;
			}
			currentFiles [e.Filename] = e.Text;
			var response = await RoslynCodeManager.Shared.SearchForPartialClasses(e.Filename, e.Text, CurrentProjectPath, Solution);
			if (response != null)
			{
				Log($"Hot Reloading: {e.Filename}");
				Log("Sending Data to the client");
				currentMessages.Add(response);
                await server.Send(response);
			}
		}

		public Action<object> DataRecieved { get; set; }
		public string CurrentProjectPath { get; set; }
		public Solution Solution { get; internal set; }

		public void StartMonitoring ()
		{
			StartMonitoring (Constants.DEFAULT_PORT);
		}

		internal async void StartMonitoring (int port)
		{
			try
			{
				RoslynCodeManager.Shared.StartDebugging();
				await server.StartListening(port);
				Log("Listening for clients");
			}
			catch(Exception ex)
			{
				Log($"Error starting server: {ex}");
			}
		}
		public void StopMonitoring ()
		{
			server.StopListening ();
			currentFiles.Clear ();
			RoslynCodeManager.Shared.StopDebugging();
			Log("Server Shutdown");
		}

		public void Log(string log)
		{
			LogAction?.Invoke(log);
		}
	}
}
