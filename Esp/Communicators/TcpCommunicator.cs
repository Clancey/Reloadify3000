using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Esp;
using Esp.Resources;
using Newtonsoft.Json.Linq;

namespace Esp {
	public class TcpCommunicatorServer : TcpCommunicator, ITcpCommunicatorServer {

		protected ConcurrentDictionary<Guid, Tuple<TcpClient, CancellationTokenSource>> clients = new ConcurrentDictionary<Guid, Tuple<TcpClient, CancellationTokenSource>> ();
		int serverPort;
		TcpListener listener;

		public event EventHandler<ClientConnectedEventArgs> ClientConnected;

		public int ClientsCount => clients.Count;

		public Task<bool> StartListening (int serverPort)
		{
			this.serverPort = serverPort;
			var taskCompletion = new TaskCompletionSource<bool> ();
			Task.Factory.StartNew (() => Run (taskCompletion), TaskCreationOptions.LongRunning);
			return taskCompletion.Task;
		}

		async Task Run (TaskCompletionSource<bool> tcs)
		{
			try {
				StopListening();
				listener = new TcpListener (IPAddress.Any, serverPort);
				listener.Start ();
			} catch (Exception ex) {
				tcs.SetException (ex);
				return;
			}
			Debug.WriteLine ($"Tcp server listening at port {serverPort}");
			tcs.SetResult (true);

			// Loop
			for (; ; )
			{
				var client = await listener.AcceptTcpClientAsync ();
				var token = new CancellationTokenSource ();
				Receive (client, token.Token);
				var guid = Guid.NewGuid ();
				clients [guid] = new Tuple<TcpClient, CancellationTokenSource> (client, token);

				await Task.Run (async () => {
					await Task.Delay (100);
					await SendToClient (client, GetBytesForObject (new ConnectMessage { ClientId = guid.ToString () }));
				});
				Debug.WriteLine ($"New client connection: {guid}");
				ClientConnected?.Invoke (this, new ClientConnectedEventArgs { ClientId = guid});
			}
		}

		public async Task<bool> SendToClient<T>(Guid clientId,T obj)
        {
			try
			{
				var client = clients[clientId].Item1;
				byte[] bytesToSend = GetBytesForObject(obj);
				await client.GetStream().WriteAsync(bytesToSend, 0, bytesToSend.Length);
				return true;
			}
			catch(Exception ex)
            {
				Console.WriteLine(ex);
				return false;
            }
        }


        public void StopListening ()
		{
			foreach (var client in clients) {
				client.Value.Item1.Close ();
				client.Value.Item2.Cancel ();
			}
			clients.Clear ();
			listener?.Stop ();
		}

		public override async Task<bool> Send<T> (T obj)
		{
			byte [] bytesToSend = GetBytesForObject (obj);
			foreach (var client in clients) {

				bool isConnected = client.Value.Item1.Connected;
				if (isConnected) {
					Debug.WriteLine ($"Sending to:{client.Key}");
					try {
						await SendToClient (client.Value.Item1,bytesToSend);
					} catch {
						isConnected = false;
					}
				}

				if (!isConnected) {
					Debug.WriteLine ($"Failed to send to:{client.Key}");
					client.Value.Item1.Close ();
					clients.TryRemove (client.Key, out Tuple<TcpClient, CancellationTokenSource> removedClient);
					removedClient?.Item2.Cancel ();
				}
			}
			//Improve return if errors
			return true;
		}

		protected override bool ProcessMessageInternal (JContainer container)
		{
			string type = (string)container ["Type"];

			if (type == typeof (DiscconectMessage).Name) {
				var connect = container.ToObject<DiscconectMessage> ();
				var id = Guid.Parse (connect.ClientId);
				clients.TryRemove(id, out var client);
				client.Item1?.Close ();
				client.Item2?.Cancel ();
				Console.WriteLine ($"Disconected Client: {ClientConnected} ");
				return true;
			}
			return false;
		}
	}

	public class TcpCommunicatorClient : TcpCommunicator, ITcpCommunicatorClient {

		string identifier;
		public static List<TcpCommunicatorClient> GetTcpCommunicatorsFromResource ()
		{
			var ips = GetIdeIPFromResource ();
			return ips.Where (x => !string.IsNullOrWhiteSpace (x)).Select (ip => new TcpCommunicatorClient { Ip = ip, Port = Constants.DEFAULT_PORT }).ToList ();
		}
		static List<string> GetIdeIPFromResource ()
		{
			try {
				using (Stream stream = typeof (Esp.Resources.Constants).Assembly.GetManifestResourceStream (Constants.IDE_IP_RESOURCE_NAME))
				using (StreamReader reader = new StreamReader (stream)) {
					var ips = reader.ReadToEnd ().Split ('\n').ToList ();
					var loopBack = IPAddress.Loopback.ToString ();
					if (!ips.Contains (loopBack))
						ips.Insert(0,loopBack);
					return ips;
				}
			} catch (Exception ex) {
				Debug.WriteLine (ex);
				return null;
			}
		}

		TcpClient client;

		public string Ip { get; set; }
		public int Port { get; set; }
		CancellationTokenSource readCancellationToken;
		public Task<bool> Connect (string ip, int port) => Connect (ip, port, CancellationToken.None);
		public async Task<bool> Connect (string ip, int port, CancellationToken cancellationToken)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(ip))
				{
					throw new ArgumentException("Ip has not been set", nameof(ip));
				}
				Ip = ip;
				Port = port;
				await Disconnect();
				ShouldBeConnected = true;
				client = new TcpClient();
				await client.ConnectAsync(ip, port);
				readCancellationToken = new CancellationTokenSource();
				if (cancellationToken.IsCancellationRequested)
				{
					await Disconnect();
					return false;
				}
				Receive(client, readCancellationToken.Token);
				return true;
			}
			catch(Exception)
			{
				return false;
			}
		}

		public async Task Disconnect ()
		{
			readCancellationToken?.Cancel ();
			ShouldBeConnected = false;
			if(client?.Connected ?? false)
				await Send (new DiscconectMessage { ClientId = identifier });
			client?.Close ();
			client?.Dispose ();
		}

		async Task<(bool, ICommunicatorClient)> ICommunicatorClient.Connect (CancellationToken cancellationToken) =>
			(await Connect (Ip, Port, cancellationToken), this);

		public override async Task<bool> Send<T> (T obj)
		{
			if (client?.Connected ?? false) {
				try {
					await SendToClient (client, GetBytesForObject (obj));
					return true;
				} catch(Exception ex) {
					Debug.WriteLine (ex);
				}
			}
			return false;
		}

		protected override bool ProcessMessageInternal (JContainer container)
		{
			string type = (string)container ["Type"];

			if (type == typeof (ConnectMessage).Name) {
				var connect = container.ToObject<ConnectMessage> ();
				identifier = connect.ClientId;
				return true;
			}
			return false;
		}
	}

	public abstract class TcpCommunicator : ICommunicator {

		protected bool ShouldBeConnected = true;
		string pendingmsg;

		public Action<object> DataReceived { get; set; }

		public abstract Task<bool> Send<T> (T obj);

		public static byte [] GetBytesForObject<T> (T obj)
		{
			var json = Serializer.SerializeJson (obj);
			json += '\0';
			var encoding = new UTF8Encoding (false);
			var bytesToSend = encoding.GetBytes (json);
			return bytesToSend;
		}

		public async Task SendToClient (TcpClient client, byte [] bytesToSend)
		{
			await client.GetStream ().WriteAsync (bytesToSend, 0, bytesToSend.Length);
		}

		protected void Receive (TcpClient client, CancellationToken cancellationToken)
		{
			Debug.WriteLine ("Start receiving updates from ide");
			Task.Run (async () => {
				await ReceiveLoop (client, cancellationToken);
			}, cancellationToken);
		}
		protected abstract bool ProcessMessageInternal (JContainer container);

		async Task ReceiveLoop (TcpClient client, CancellationToken cancellationToken)
		{
			try {
				byte [] bytes = new byte [1024];
				int bytesRead = 0;
				// Loop to receive all the data sent by the client.
				bytesRead = await client.GetStream ().ReadAsync (bytes, 0, bytes.Length, cancellationToken);
				Console.WriteLine ("Recieved Data");
				while (bytesRead != 0) {
					if (!ShouldBeConnected)
						return;
					// Translate data bytes to a UTF8 string.
					string msg;
					msg = Encoding.UTF8.GetString (bytes, 0, bytesRead);

					// Process the data sent by the client.
					if (pendingmsg != null) {
						msg = pendingmsg + msg;
						pendingmsg = null;
					}
					int t = msg.LastIndexOf ('\0');
					if (t == -1) {
						pendingmsg = msg;
						msg = null;
					} else if (t != msg.Length - 1) {
						pendingmsg = msg.Substring (t + 1, msg.Length - t - 1);
						msg = msg.Substring (0, t);
					}
					if (msg != null) {
						var msgs = msg.Split ('\0');
						foreach (var ms in msgs) {
							if (!string.IsNullOrWhiteSpace (ms)) {
								try {
									var message = Serializer.DeserializeJson (ms);
									if (message is JContainer container && ProcessMessageInternal (container)) {
										//We handled this internally!
									} else 
										DataReceived?.Invoke (Serializer.DeserializeJson (ms));
								} catch (Exception ex) {
									Debug.WriteLine (ex);
								}
							}
						}
					}
					//Receive more bytes
					bytesRead = await client.GetStream ().ReadAsync (bytes, 0, bytes.Length, cancellationToken);
				}
			} catch (Exception ex) {
				Console.WriteLine (ex);
			}

			Debug.WriteLine ("Receive stopped, disconnected");
		}
	}
}
