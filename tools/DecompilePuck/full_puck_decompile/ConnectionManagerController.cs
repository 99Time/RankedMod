using System.Collections.Generic;
using System.Text.Json;
using Unity.Multiplayer.Tools.NetStatsMonitor;
using Unity.Netcode;
using UnityEngine;

public class ConnectionManagerController : MonoBehaviour
{
	private ConnectionManager connectionManager;

	private RuntimeNetStatsMonitor runtimeNetStatsMonitor;

	private void Awake()
	{
		connectionManager = GetComponent<ConnectionManager>();
		runtimeNetStatsMonitor = GetComponent<RuntimeNetStatsMonitor>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnClientConnected", Event_OnClientConnected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnIsClientChanged", Event_Client_OnIsClientChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMainMenuClickJoinServer", Event_Client_OnMainMenuClickJoinServer);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPauseMenuClickDisconnect", Event_Client_OnPauseMenuClickDisconnect);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnDebugChanged", Event_Client_OnDebugChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnGotLaunchCommandLine", Event_Client_OnGotLaunchCommandLine);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnGameRichPresenceJoinRequested", Event_Client_OnGameRichPresenceJoinRequested);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnServerBrowserClickServer", Event_Client_OnServerBrowserClickServer);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPendingModsCleared", Event_Client_OnPendingModsCleared);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPopupClickOk", Event_Client_OnPopupClickOk);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnClientConnected", Event_OnClientConnected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnIsClientChanged", Event_Client_OnIsClientChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMainMenuClickJoinServer", Event_Client_OnMainMenuClickJoinServer);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPauseMenuClickDisconnect", Event_Client_OnPauseMenuClickDisconnect);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnDebugChanged", Event_Client_OnDebugChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnGotLaunchCommandLine", Event_Client_OnGotLaunchCommandLine);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnGameRichPresenceJoinRequested", Event_Client_OnGameRichPresenceJoinRequested);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnServerBrowserClickServer", Event_Client_OnServerBrowserClickServer);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPendingModsCleared", Event_Client_OnPendingModsCleared);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPopupClickOk", Event_Client_OnPopupClickOk);
	}

	private void Event_OnClientConnected(Dictionary<string, object> message)
	{
		ulong num = (ulong)message["clientId"];
		if (NetworkManager.Singleton.LocalClientId == num)
		{
			connectionManager.IsConnecting = false;
			connectionManager.PendingConnection = null;
		}
	}

	private void Event_OnClientDisconnected(Dictionary<string, object> message)
	{
		ulong num = (ulong)message["clientId"];
		if (NetworkManager.Singleton.LocalClientId == num)
		{
			if (connectionManager.IsConnecting)
			{
				connectionManager.IsConnecting = false;
				ConnectionRejection connectionRejection = ((!string.IsNullOrEmpty(NetworkManager.Singleton.DisconnectReason)) ? JsonSerializer.Deserialize<ConnectionRejection>(NetworkManager.Singleton.DisconnectReason) : new ConnectionRejection
				{
					code = ConnectionRejectionCode.Unreachable
				});
				Debug.Log("[ConnectionManagerController] Connection rejected: " + Utils.GetConnectionRejectionMessage(connectionRejection.code));
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnConnectionRejected", new Dictionary<string, object> { { "connectionRejection", connectionRejection } });
			}
			else
			{
				Disconnection disconnection = ((!string.IsNullOrEmpty(NetworkManager.Singleton.DisconnectReason)) ? JsonSerializer.Deserialize<Disconnection>(NetworkManager.Singleton.DisconnectReason) : new Disconnection
				{
					code = DisconnectionCode.Disconnected
				});
				Debug.Log("[ConnectionManagerController] Disconnected: " + Utils.GetDisconnectionMessage(disconnection.code));
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnDisconnected", new Dictionary<string, object> { { "disconnection", disconnection } });
			}
		}
	}

	private void Event_Client_OnIsClientChanged(Dictionary<string, object> message)
	{
		if (!NetworkManager.Singleton.IsClient && connectionManager.IsPendingConnection)
		{
			string ipAddress = connectionManager.PendingConnection.IpAddress;
			ushort port = connectionManager.PendingConnection.Port;
			string password = connectionManager.PendingConnection.Password;
			connectionManager.PendingConnection = null;
			connectionManager.Client_StartClient(ipAddress, port, password);
		}
	}

	private void Event_Client_OnMainMenuClickJoinServer(Dictionary<string, object> message)
	{
		string ipAddress = (string)message["ip"];
		ushort port = (ushort)message["port"];
		string password = (string)message["password"];
		connectionManager.Client_StartClient(ipAddress, port, password);
	}

	private void Event_Client_OnPauseMenuClickDisconnect(Dictionary<string, object> message)
	{
		connectionManager.Client_Disconnect();
	}

	private void Event_Client_OnDebugChanged(Dictionary<string, object> message)
	{
		int num = (int)message["value"];
		NetworkManager.Singleton.NetworkConfig.NetworkMessageMetrics = num > 0;
		NetworkManager.Singleton.NetworkConfig.NetworkProfilingMetrics = num > 0;
		runtimeNetStatsMonitor.Visible = num > 0;
	}

	private void Event_Client_OnGotLaunchCommandLine(Dictionary<string, object> message)
	{
		string[] args = (string[])message["args"];
		string commandLineArgument = Utils.GetCommandLineArgument("+ipAddress", args);
		ushort result;
		ushort port = (ushort)(ushort.TryParse(Utils.GetCommandLineArgument("+port", args), out result) ? result : 7777);
		string commandLineArgument2 = Utils.GetCommandLineArgument("+password", args);
		if (!string.IsNullOrEmpty(commandLineArgument))
		{
			connectionManager.Client_StartClient(commandLineArgument, port, commandLineArgument2);
		}
	}

	private void Event_Client_OnGameRichPresenceJoinRequested(Dictionary<string, object> message)
	{
		string[] args = (string[])message["args"];
		string commandLineArgument = Utils.GetCommandLineArgument("+ipAddress", args);
		ushort result;
		ushort port = (ushort)(ushort.TryParse(Utils.GetCommandLineArgument("+port", args), out result) ? result : 7777);
		string commandLineArgument2 = Utils.GetCommandLineArgument("+password", args);
		if (!string.IsNullOrEmpty(commandLineArgument))
		{
			connectionManager.Client_StartClient(commandLineArgument, port, commandLineArgument2);
		}
	}

	private void Event_Client_OnServerBrowserClickServer(Dictionary<string, object> message)
	{
		ServerBrowserServer serverBrowserServer = (ServerBrowserServer)message["serverBrowserServer"];
		connectionManager.Client_StartClient(serverBrowserServer.ipAddress, serverBrowserServer.port);
	}

	private void Event_Client_OnPendingModsCleared(Dictionary<string, object> message)
	{
		Debug.Log($"[ConnectionManagerController] Pending mods cleared, reconnecting to last connection ({connectionManager.LastConnection.IpAddress}:{connectionManager.LastConnection.Port})");
		connectionManager.Client_StartClient(connectionManager.LastConnection.IpAddress, connectionManager.LastConnection.Port, connectionManager.LastConnection.Password);
	}

	private void Event_Client_OnPopupClickOk(Dictionary<string, object> message)
	{
		if (!((string)message["name"] != "missingPassword"))
		{
			string password = ((PopupContentPassword)message["content"]).Password;
			connectionManager.Client_StartClient(connectionManager.LastConnection.IpAddress, connectionManager.LastConnection.Port, password);
		}
	}
}
