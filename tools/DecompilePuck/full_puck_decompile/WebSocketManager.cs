using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SocketIOClient;
using SocketIOClient.Transport.Http;
using UnityEngine;

public class WebSocketManager : MonoBehaviourSingleton<WebSocketManager>
{
	private string uri;

	private SocketIO socket;

	private SocketIOOptions options = new SocketIOOptions
	{
		ReconnectionDelay = 2500.0,
		ReconnectionDelayMax = 5000
	};

	private CancellationTokenSource cancellationTokenSource;

	private readonly Dictionary<string, Action<Dictionary<string, object>>> events = new Dictionary<string, Action<Dictionary<string, object>>>();

	[HideInInspector]
	public string SocketId => socket.Id;

	public override void Awake()
	{
		base.Awake();
	}

	private void Start()
	{
		Connect("wss://puck1.nasejevs.com");
	}

	private async void Connect(string uri)
	{
		this.uri = uri;
		socket = new SocketIO(this.uri, options);
		socket.OnConnected += OnConnected;
		socket.OnDisconnected += OnDisconnected;
		socket.OnError += OnError;
		socket.OnPing += OnPing;
		socket.OnPong += OnPong;
		socket.OnReconnectAttempt += OnReconnectAttempt;
		socket.OnReconnected += OnReconnected;
		socket.OnReconnectError += OnReconnectError;
		socket.OnReconnectFailed += OnReconnectFailed;
		socket.OnAny(OnAny);
		Debug.Log("[WebSocketManager] WebSocket connecting to " + this.uri + "...");
		try
		{
			cancellationTokenSource = new CancellationTokenSource();
			await socket.ConnectAsync(cancellationTokenSource.Token);
		}
		catch (Exception ex)
		{
			Debug.Log("[WebSocketManager] WebSocket connection error: " + ex.Message);
		}
	}

	private Task Disconnect()
	{
		if (socket == null)
		{
			return Task.CompletedTask;
		}
		Debug.Log("[WebSocketManager] WebSocket disconnecting from " + uri + "...");
		socket.OnConnected -= OnConnected;
		socket.OnDisconnected -= OnDisconnected;
		socket.OnError -= OnError;
		socket.OnPing -= OnPing;
		socket.OnPong -= OnPong;
		socket.OnReconnectAttempt -= OnReconnectAttempt;
		socket.OnReconnected -= OnReconnected;
		socket.OnReconnectError -= OnReconnectError;
		socket.OnReconnectFailed -= OnReconnectFailed;
		socket.OffAny(OnAny);
		if (socket.Connected)
		{
			return socket.DisconnectAsync();
		}
		cancellationTokenSource.Cancel();
		return Task.CompletedTask;
	}

	public void Emit(string messageName, Dictionary<string, object> data = null, string responseMessageName = null)
	{
		MonoBehaviourSingleton<ThreadManager>.Instance.Enqueue(delegate
		{
			string text = ((responseMessageName != null) ? " request " : " ");
			Debug.Log("[WebSocketManager] WebSocket sent" + text + "message " + messageName + " " + JsonSerializer.Serialize(data));
			Action<SocketIOResponse> ack = delegate(SocketIOResponse response)
			{
				Debug.Log($"[WebSocketManager] WebSocket received response message {responseMessageName} {response}");
				if (responseMessageName != null)
				{
					TriggerMessage(responseMessageName, new Dictionary<string, object> { { "response", response } });
				}
			};
			socket.EmitAsync(messageName, ack, data);
			TriggerMessage("emit", new Dictionary<string, object> { { "messageName", messageName } });
		});
	}

	private void OnConnected(object sender, EventArgs args)
	{
		Debug.Log("[WebSocketManager] WebSocket connected to " + uri);
		TriggerMessage("connect", new Dictionary<string, object> { { "socket", socket } });
	}

	private void OnDisconnected(object sender, string reason)
	{
		Debug.Log("[WebSocketManager] WebSocket disconnected (" + reason + ")");
		TriggerMessage("disconnect");
	}

	private void OnError(object sender, string error)
	{
		Debug.Log("[WebSocketManager] WebSocket error: " + error);
	}

	private void OnPing(object sender, EventArgs args)
	{
	}

	private void OnPong(object sender, TimeSpan timeSpan)
	{
	}

	private void OnReconnectAttempt(object sender, int attempt)
	{
		Debug.Log($"[WebSocketManager] WebSocket reconnect attempt: {attempt}");
	}

	private void OnReconnected(object sender, int attempt)
	{
		Debug.Log($"[WebSocketManager] WebSocket reconnected on attempt {attempt}");
	}

	private void OnReconnectError(object sender, Exception exception)
	{
		Debug.Log("[WebSocketManager] WebSocket reconnect error: " + exception.Message);
		if (exception.ToString().Contains("UNITYTLS_X509VERIFY_FLAG_NOT_TRUSTED"))
		{
			Debug.Log("[WebSocketManager] UNITYTLS_X509VERIFY_FLAG_NOT_TRUSTED, disconnecting...");
			options.AutoUpgrade = false;
			options.RemoteCertificateValidationCallback = (object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;
			socket.Options.AutoUpgrade = options.AutoUpgrade;
			socket.HttpClient = new DefaultHttpClient(options.RemoteCertificateValidationCallback);
		}
	}

	private void OnReconnectFailed(object sender, EventArgs args)
	{
		Debug.Log($"[WebSocketManager] WebSocket reconnect failed {args}");
	}

	private void OnAny(string messageName, SocketIOResponse response)
	{
		Debug.Log($"[WebSocketManager] WebSocket received message {messageName} {response}");
		TriggerMessage(messageName, new Dictionary<string, object> { { "response", response } });
	}

	public void AddMessageListener(string messageName, Action<Dictionary<string, object>> listener)
	{
		if (events.ContainsKey(messageName))
		{
			Dictionary<string, Action<Dictionary<string, object>>> dictionary = events;
			dictionary[messageName] = (Action<Dictionary<string, object>>)Delegate.Combine(dictionary[messageName], listener);
		}
		else
		{
			Action<Dictionary<string, object>> a = null;
			a = (Action<Dictionary<string, object>>)Delegate.Combine(a, listener);
			events.Add(messageName, a);
		}
	}

	public void RemoveMessageListener(string messageName, Action<Dictionary<string, object>> listener)
	{
		if (events.ContainsKey(messageName))
		{
			Dictionary<string, Action<Dictionary<string, object>>> dictionary = events;
			dictionary[messageName] = (Action<Dictionary<string, object>>)Delegate.Remove(dictionary[messageName], listener);
		}
	}

	public void TriggerMessage(string messageName, Dictionary<string, object> message = null)
	{
		MonoBehaviourSingleton<ThreadManager>.Instance.Enqueue(delegate
		{
			if (events.ContainsKey(messageName))
			{
				events[messageName]?.Invoke(message);
			}
		});
	}

	private async void OnApplicationQuit()
	{
		await Disconnect();
	}
}
