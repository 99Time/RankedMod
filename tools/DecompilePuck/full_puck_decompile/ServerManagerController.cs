using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SocketIOClient;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class ServerManagerController : NetworkBehaviour
{
	private ServerManager serverManager;

	private List<ulong> approvedClients = new List<ulong>();

	private void Awake()
	{
		serverManager = GetComponent<ServerManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnClientConnected", Event_OnClientConnected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerRemoved", Event_OnPlayerRemoved);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnServerStarted", Event_Server_OnServerStarted);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnServerReady", Event_Server_OnServerReady);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnServerStopped", Event_Server_OnServerStopped);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_ConnectionApproval", Event_Server_ConnectionApproval);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnVoteSuccess", Event_Server_OnVoteSuccess);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnPlayerSubscription", Event_Server_OnPlayerSubscription);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnChatCommand", Event_Server_OnChatCommand);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnPlayerSleepInput", Event_Server_OnPlayerSleepInput);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnTransportFailure", Event_Client_OnTransportFailure);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnLevelReady", Event_Client_OnLevelReady);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMainMenuClickHostServer", Event_Client_OnMainMenuClickHostServer);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMainMenuClickPractice", Event_Client_OnMainMenuClickPractice);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnServerLauncherClickStartSelfHostedServer", Event_Client_OnServerLauncherClickStartSelfHostedServer);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("connect", WebSocket_Event_OnConnect);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("serverAuthenticateResponse", WebSocket_Event_OnServerAuthenticateResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("serverConnectionApprovalResponse", WebSocket_Event_OnServerConnectionApprovalResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("serverKickPlayer", WebSocket_Event_OnServerKickPlayer);
	}

	public void HelloWorld()
	{
		Debug.Log("[ServerManagerController] Hello World");
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnClientConnected", Event_OnClientConnected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerRemoved", Event_OnPlayerRemoved);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnServerStarted", Event_Server_OnServerStarted);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnServerReady", Event_Server_OnServerReady);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnServerStopped", Event_Server_OnServerStopped);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_ConnectionApproval", Event_Server_ConnectionApproval);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnVoteSuccess", Event_Server_OnVoteSuccess);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnPlayerSubscription", Event_Server_OnPlayerSubscription);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnChatCommand", Event_Server_OnChatCommand);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnPlayerSleepInput", Event_Server_OnPlayerSleepInput);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnTransportFailure", Event_Client_OnTransportFailure);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnLevelReady", Event_Client_OnLevelReady);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMainMenuClickHostServer", Event_Client_OnMainMenuClickHostServer);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMainMenuClickPractice", Event_Client_OnMainMenuClickPractice);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnServerLauncherClickStartSelfHostedServer", Event_Client_OnServerLauncherClickStartSelfHostedServer);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("connect", WebSocket_Event_OnConnect);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("serverAuthenticateResponse", WebSocket_Event_OnServerAuthenticateResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("serverConnectionApprovalResponse", WebSocket_Event_OnServerConnectionApprovalResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("serverKickPlayer", WebSocket_Event_OnServerKickPlayer);
		base.OnDestroy();
	}

	private void Event_OnClientConnected(Dictionary<string, object> message)
	{
		ulong num = (ulong)message["clientId"];
		if (NetworkManager.Singleton.IsServer)
		{
			Debug.Log($"[ServerManagerController] Client connected ({num}) {NetworkManager.Singleton.ConnectedClientsList.Count}/{serverManager.Server.MaxPlayers}");
			if (serverManager.EdgegapManager.IsEdgegap)
			{
				serverManager.EdgegapManager.StopDeleteDeploymentCoroutine();
			}
		}
	}

	private void Event_OnClientDisconnected(Dictionary<string, object> message)
	{
		ulong num = (ulong)message["clientId"];
		if (!NetworkManager.Singleton.IsServer)
		{
			return;
		}
		Debug.Log($"[ServerManagerController] Client disconnected ({num}) {NetworkManager.Singleton.ConnectedClientsList.Count}/{serverManager.Server.MaxPlayers}");
		if (approvedClients.Contains(num))
		{
			approvedClients.Remove(num);
			if (serverManager.EdgegapManager.IsEdgegap && approvedClients.Count == 0)
			{
				serverManager.EdgegapManager.DeleteDeployment();
			}
		}
	}

	private void Event_Client_OnTransportFailure(Dictionary<string, object> message)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnServerStartFailed", new Dictionary<string, object>
		{
			{ "isHost", serverManager.IsHostStartInProgress },
			{ "isServer", serverManager.IsServerStartInProgress }
		});
	}

	private void Event_Client_OnLevelReady(Dictionary<string, object> message)
	{
		if (serverManager.IsServerStartInProgress)
		{
			NetworkManager.Singleton.StartServer();
			serverManager.IsServerStartInProgress = false;
		}
		if (serverManager.IsHostStartInProgress)
		{
			NetworkManager.Singleton.StartHost();
			serverManager.IsHostStartInProgress = false;
		}
	}

	private void Event_OnPlayerRemoved(Dictionary<string, object> message)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			string[] playerSteamIds = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerSteamIds();
			serverManager.Server_UpdateConnectedSteamIds(playerSteamIds);
		}
	}

	private void Event_Server_OnServerStarted(Dictionary<string, object> message)
	{
		serverManager.UdpSocket.StartSocket(serverManager.ServerConfigurationManager.ServerConfiguration.pingPort);
		NetworkBehaviourSingleton<SynchronizedObjectManager>.Instance.TickRate = serverManager.ServerConfigurationManager.ServerConfiguration.serverTickRate;
		NetworkBehaviourSingleton<GameManager>.Instance.PhaseDurationMap = serverManager.ServerConfigurationManager.ServerConfiguration.phaseDurationMap;
		serverManager.LoadAdminSteamIds();
		serverManager.LoadBannedSteamIds();
		if (serverManager.ServerConfigurationManager.ServerConfiguration.printMetrics)
		{
			serverManager.Server_StartMetricsCoroutine();
		}
		if (serverManager.ServerConfigurationManager.ServerConfiguration.reloadBannedSteamIds)
		{
			serverManager.Server_StartBannedSteamIdsReloadCoroutine();
		}
		if (Application.isBatchMode)
		{
			Application.targetFrameRate = serverManager.ServerConfigurationManager.ServerConfiguration.targetFrameRate;
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnServerReady");
	}

	private void Event_Server_OnServerReady(Dictionary<string, object> message)
	{
		serverManager.Server_Authenticate(new string[0]);
	}

	private void Event_Server_OnServerStopped(Dictionary<string, object> message)
	{
		serverManager.Server_StopMetricsCoroutine();
		serverManager.Server_StopBannedSteamIdsReloadCoroutine();
		serverManager.Server_Unauthenticate();
		serverManager.UdpSocket.StopSocket();
		serverManager.ConnectionApprovalRequests.Clear();
		uPnPHelper.CloseAll();
	}

	private void Event_Server_ConnectionApproval(Dictionary<string, object> message)
	{
		ulong item = (ulong)message["clientId"];
		if ((bool)message["approved"])
		{
			approvedClients.Add(item);
		}
	}

	private void Event_Server_OnVoteSuccess(Dictionary<string, object> message)
	{
		Vote vote = (Vote)message["vote"];
		if (vote.Type == VoteType.Kick)
		{
			FixedString32Bytes steamId = (FixedString32Bytes)vote.Data;
			Player playerBySteamId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(steamId);
			if ((bool)playerBySteamId)
			{
				Debug.Log($"[ServerManagerController] Vote succeeded to kick player {playerBySteamId.Username.Value} ({playerBySteamId.OwnerClientId}) ({vote.Votes}/{vote.VotesNeeded})");
				serverManager.Server_KickPlayer(playerBySteamId);
			}
		}
	}

	private void Event_Server_OnPlayerSubscription(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (!player.IsReplay.Value)
		{
			string[] playerSteamIds = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerSteamIds();
			serverManager.Server_UpdateConnectedSteamIds(playerSteamIds);
			if (player.OwnerClientId != 0L)
			{
				serverManager.Server_ServerConfigurationRpc(serverManager.Server, base.RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
			}
		}
	}

	private void Event_Server_OnChatCommand(Dictionary<string, object> message)
	{
		ulong clientId = (ulong)message["clientId"];
		string text = (string)message["command"];
		string[] array = (string[])message["args"];
		Player playerByClientId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId);
		if (!playerByClientId)
		{
			return;
		}
		bool flag = serverManager.AdminSteamIds.Contains(playerByClientId.SteamId.Value.ToString());
		switch (text)
		{
		case "/kick":
		{
			if (!flag || array.Length < 1)
			{
				break;
			}
			Player player2 = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByUsername(array[0], caseSensitive: false);
			if (!player2)
			{
				if (int.TryParse(array[0], out var result2))
				{
					player2 = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByNumber(result2);
				}
				if (!player2)
				{
					break;
				}
			}
			NetworkBehaviourSingleton<UIChat>.Instance.Server_SendSystemChatMessage($"<b><color=orange>ADMIN</color></b> kicked {player2.Username.Value}.");
			serverManager.Server_KickPlayer(player2);
			break;
		}
		case "/ban":
		{
			if (!flag || array.Length < 1)
			{
				break;
			}
			Player player = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByUsername(array[0], caseSensitive: false);
			if (!player)
			{
				if (int.TryParse(array[0], out var result))
				{
					player = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByNumber(result);
				}
				if (!player)
				{
					break;
				}
			}
			NetworkBehaviourSingleton<UIChat>.Instance.Server_SendSystemChatMessage($"<b><color=orange>ADMIN</color></b> banned {player.Username.Value}.");
			serverManager.Server_BanPlayer(player);
			break;
		}
		case "/bansteamid":
			if (flag && array.Length >= 1)
			{
				string text3 = array[0];
				serverManager.Server_BanSteamId(text3);
				NetworkBehaviourSingleton<UIChat>.Instance.Server_SendSystemChatMessage("<b><color=orange>ADMIN</color></b> banned Steam ID " + text3 + ".");
			}
			break;
		case "/unbansteamid":
			if (flag && array.Length >= 1)
			{
				string text2 = array[0];
				serverManager.Server_UnbanSteamId(text2);
				NetworkBehaviourSingleton<UIChat>.Instance.Server_SendSystemChatMessage("<b><color=orange>ADMIN</color></b> unbanned Steam ID " + text2 + ".");
			}
			break;
		}
	}

	private void Event_Server_OnPlayerSleepInput(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if ((bool)message["value"])
		{
			serverManager.Server_KickPlayer(player, DisconnectionCode.Kicked, applyTimeout: false);
		}
	}

	private void Event_Client_OnMainMenuClickHostServer(Dictionary<string, object> message)
	{
		ushort port = (ushort)message["port"];
		string password = (string)message["password"];
		serverManager.Client_StartHost(port, "MY PUCK SERVER", 12, password, voip: true, isPublic: false, MonoBehaviourSingleton<StateManager>.Instance.PlayerData.steamId, uPnP: true);
	}

	private void Event_Client_OnMainMenuClickPractice(Dictionary<string, object> message)
	{
		serverManager.Client_StartHost(7777, "PRACTICE", 1, "", voip: false, isPublic: false, MonoBehaviourSingleton<StateManager>.Instance.PlayerData.steamId);
	}

	private void Event_Client_OnServerLauncherClickStartSelfHostedServer(Dictionary<string, object> message)
	{
		int num = (int)message["port"];
		string text = (string)message["name"];
		int maxPlayers = (int)message["maxPlayers"];
		string password = (string)message["password"];
		bool voip = (bool)message["voip"];
		serverManager.Client_StartHost((ushort)num, text, maxPlayers, password, voip, isPublic: true, MonoBehaviourSingleton<StateManager>.Instance.PlayerData.steamId, uPnP: true);
	}

	private void WebSocket_Event_OnConnect(Dictionary<string, object> message)
	{
		if ((bool)NetworkManager.Singleton)
		{
			if (Application.isBatchMode && !NetworkManager.Singleton.IsServer)
			{
				serverManager.Client_StartServer(serverManager.ServerConfigurationManager.ServerConfiguration.port, uPnP: true);
			}
			if (NetworkManager.Singleton.IsServer)
			{
				serverManager.Server_Authenticate(NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerSteamIds());
			}
		}
		else if (Application.isBatchMode)
		{
			serverManager.Client_StartServer(serverManager.ServerConfigurationManager.ServerConfiguration.port, uPnP: true);
		}
	}

	private void WebSocket_Event_OnServerAuthenticateResponse(Dictionary<string, object> message)
	{
		ServerAuthenticateResponse value = ((SocketIOResponse)message["response"]).GetValue<ServerAuthenticateResponse>();
		if (value.success)
		{
			serverManager.Server.IpAddress = value.ipAddress;
			serverManager.Server.IsAuthenticated = value.isAuthenticated;
			Debug.Log($"[ServerManagerController] Server authenticated {serverManager.Server.IpAddress} {serverManager.Server.IsAuthenticated}");
			if (NetworkManager.Singleton.IsHost)
			{
				serverManager.Server_ServerConfigurationRpc(serverManager.Server, base.RpcTarget.Single(0uL, RpcTargetUse.Temp));
			}
		}
	}

	private void WebSocket_Event_OnServerConnectionApprovalResponse(Dictionary<string, object> message)
	{
		string steamId = ((SocketIOResponse)message["response"]).GetValue<ServerConnectionApprovalResponse>().steamId;
		if (!serverManager.ConnectionApprovalRequests.ContainsKey(steamId))
		{
			return;
		}
		NetworkManager.ConnectionApprovalResponse connectionApprovalResponse = serverManager.ConnectionApprovalRequests[steamId];
		bool flag = NetworkManager.Singleton.ConnectedClientsList.Count >= serverManager.Server.MaxPlayers;
		connectionApprovalResponse.Approved = !flag;
		if (connectionApprovalResponse.Approved)
		{
			connectionApprovalResponse.Pending = false;
			Debug.Log("[ServerManager] Connection approved for (" + steamId + ")");
		}
		else
		{
			ConnectionRejectionCode code = ConnectionRejectionCode.Unreachable;
			if (flag)
			{
				code = ConnectionRejectionCode.ServerFull;
			}
			string reason = JsonSerializer.Serialize(new ConnectionRejection
			{
				code = code,
				clientRequiredModIds = serverManager.ServerConfigurationManager.ClientRequiredModIds
			}, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			connectionApprovalResponse.Pending = false;
			connectionApprovalResponse.Reason = reason;
			Debug.Log("[ServerManager] Connection rejected for (" + steamId + "): " + Utils.GetConnectionRejectionMessage(code));
		}
		serverManager.ConnectionApprovalRequests.Remove(steamId);
	}

	private void WebSocket_Event_OnServerKickPlayer(Dictionary<string, object> message)
	{
		ServerKickPlayer value = ((SocketIOResponse)message["response"]).GetValue<ServerKickPlayer>();
		Player playerBySteamId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(value.steamId);
		if ((bool)playerBySteamId)
		{
			serverManager.Server_KickPlayer(playerBySteamId, DisconnectionCode.Kicked, applyTimeout: false);
		}
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		base.__initializeRpcs();
	}

	protected internal override string __getTypeName()
	{
		return "ServerManagerController";
	}
}
