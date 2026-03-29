using System.Text;
using System.Text.Json;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class ConnectionManager : MonoBehaviourSingleton<ConnectionManager>
{
	[HideInInspector]
	public UnityTransport UnityTransport;

	[HideInInspector]
	public bool IsConnecting;

	[HideInInspector]
	public Connection PendingConnection;

	[HideInInspector]
	public Connection LastConnection;

	[HideInInspector]
	public bool IsPendingConnection => PendingConnection != null;

	private void Start()
	{
		UnityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
		if (ushort.TryParse(Application.version, out var result))
		{
			Debug.Log($"[ConnectionManager] Setting NetworkConfig protocol version to {result}");
			NetworkManager.Singleton.NetworkConfig.ProtocolVersion = result;
		}
	}

	public void Client_StartClient(string ipAddress, ushort port, string password = "")
	{
		Debug.Log($"[ConnectionManager] Starting client {ipAddress}:{port}");
		if (NetworkManager.Singleton.IsClient)
		{
			PendingConnection = new Connection
			{
				IpAddress = ipAddress,
				Port = port,
				Password = password
			};
			Debug.Log("[ConnectionManager] Existing connection detected, disconnecting and setting pending connection");
			Client_Disconnect();
			return;
		}
		string s = JsonSerializer.Serialize(new ConnectionData
		{
			Password = password,
			SteamId = MonoBehaviourSingleton<StateManager>.Instance.PlayerData.steamId,
			SocketId = MonoBehaviourSingleton<WebSocketManager>.Instance.SocketId,
			EnabledModIds = MonoBehaviourSingleton<ModManagerV2>.Instance.EnabledModIds
		});
		NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.ASCII.GetBytes(s);
		LastConnection = new Connection
		{
			IpAddress = ipAddress,
			Port = port,
			Password = password
		};
		IsConnecting = true;
		UnityTransport.SetConnectionData(ipAddress, port);
		NetworkManager.Singleton.StartClient();
	}

	public void Client_Disconnect()
	{
		if (NetworkManager.Singleton.IsClient)
		{
			Debug.Log("[ConnectionManager] Puck (" + Application.version + ") network shutdown");
			NetworkManager.Singleton.Shutdown(discardMessageQueue: true);
		}
	}
}
