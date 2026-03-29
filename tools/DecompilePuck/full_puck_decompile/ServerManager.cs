using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class ServerManager : NetworkBehaviourSingleton<ServerManager>
{
	private const int SERVER_METRICS_INTERVAL = 10;

	[HideInInspector]
	public UnityTransport UnityTransport;

	[HideInInspector]
	public EdgegapManager EdgegapManager;

	[HideInInspector]
	public ServerConfigurationManager ServerConfigurationManager;

	[HideInInspector]
	public Server Server;

	[HideInInspector]
	public bool IsHostStartInProgress;

	[HideInInspector]
	public bool IsServerStartInProgress;

	[HideInInspector]
	public string[] AdminSteamIds = new string[0];

	[HideInInspector]
	public string[] BannedSteamIds = new string[0];

	[HideInInspector]
	public Dictionary<string, float> SteamIdTimeouts = new Dictionary<string, float>();

	[HideInInspector]
	public Dictionary<string, NetworkManager.ConnectionApprovalResponse> ConnectionApprovalRequests = new Dictionary<string, NetworkManager.ConnectionApprovalResponse>();

	[HideInInspector]
	public UDPSocket UdpSocket;

	private List<float> deltaTimeBuffer = new List<float>();

	private IEnumerator serverMetricsCoroutine;

	private IEnumerator bannedSteamIdsReloadCoroutine;

	public override void Awake()
	{
		base.Awake();
		EdgegapManager = GetComponent<EdgegapManager>();
		ServerConfigurationManager = GetComponent<ServerConfigurationManager>();
		UdpSocket = new UDPSocket();
		UDPSocket udpSocket = UdpSocket;
		udpSocket.OnSocketStarted = (Action<ushort>)Delegate.Combine(udpSocket.OnSocketStarted, (Action<ushort>)delegate(ushort port)
		{
			Debug.Log($"[ServerManager] UDP socket started on port {port}");
		});
		UDPSocket udpSocket2 = UdpSocket;
		udpSocket2.OnSocketFailed = (Action<ushort>)Delegate.Combine(udpSocket2.OnSocketFailed, (Action<ushort>)delegate(ushort port)
		{
			Debug.Log($"[ServerManager] UDP socket failed on port {port}");
		});
		UDPSocket udpSocket3 = UdpSocket;
		udpSocket3.OnSocketStopped = (Action)Delegate.Combine(udpSocket3.OnSocketStopped, (Action)delegate
		{
			Debug.Log("[ServerManager] UDP socket stopped");
		});
		UDPSocket udpSocket4 = UdpSocket;
		udpSocket4.OnUdpMessageReceived = (Action<string, ushort, string, long>)Delegate.Combine(udpSocket4.OnUdpMessageReceived, (Action<string, ushort, string, long>)delegate(string ipAddress, ushort port, string message, long timestamp)
		{
			if (message == "ping")
			{
				UdpSocket.Send(ipAddress, port, "pong");
			}
		});
		uPnPHelper.DebugMode = true;
		uPnPHelper.LogErrors = true;
	}

	private void Start()
	{
		UnityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
		NetworkManager.Singleton.ConnectionApprovalCallback = Server_ConnectionApproval;
	}

	private void Update()
	{
		if (base.IsSpawned && NetworkManager.Singleton.IsServer)
		{
			deltaTimeBuffer.Add(Time.deltaTime);
		}
	}

	public void LoadAdminSteamIds()
	{
		AdminSteamIds = ServerConfigurationManager.ServerConfiguration.adminSteamIds;
	}

	public void LoadBannedSteamIds()
	{
		string path = "./banned_steam_ids.json";
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 0; i < commandLineArgs.Length; i++)
		{
			if (commandLineArgs[i] == "--bannedSteamIdsPath")
			{
				path = commandLineArgs[i + 1];
			}
		}
		string text = Uri.UnescapeDataString(new Uri(Path.GetFullPath(path)).AbsolutePath);
		Debug.Log("[ServerManager] Reading banned Steam IDs file from " + text + "...");
		if (!File.Exists(text))
		{
			Debug.Log("[ServerManager] Banned Steam IDs file not found at " + text + ", creating...");
			File.AppendAllText(text, JsonSerializer.Serialize(BannedSteamIds, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
		string json = File.ReadAllText(text);
		Debug.Log("[ServerManager] Parsing banned Steam IDs...");
		BannedSteamIds = JsonSerializer.Deserialize<string[]>(json);
		Debug.Log($"[ServerManager] Loaded {BannedSteamIds.Length} banned Steam IDs");
	}

	private void AddBannedSteamId(string steamId)
	{
		LoadBannedSteamIds();
		if (!BannedSteamIds.Contains(steamId))
		{
			BannedSteamIds = BannedSteamIds.Append(steamId).ToArray();
			string text = Uri.UnescapeDataString(new Uri(Path.GetFullPath(".") + "/banned_steam_ids.json").AbsolutePath);
			Debug.Log("[ServerManager] Writing banned Steam IDs to " + text + "...");
			File.WriteAllText(text, JsonSerializer.Serialize(BannedSteamIds, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
	}

	private void RemoveBannedSteamId(string steamId)
	{
		LoadBannedSteamIds();
		if (BannedSteamIds.Contains(steamId))
		{
			BannedSteamIds = BannedSteamIds.Where((string id) => id != steamId).ToArray();
			string text = Uri.UnescapeDataString(new Uri(Path.GetFullPath(".") + "/banned_steam_ids.json").AbsolutePath);
			Debug.Log("[ServerManager] Writing banned Steam IDs to " + text + "...");
			File.WriteAllText(text, JsonSerializer.Serialize(BannedSteamIds, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
	}

	public void Server_Authenticate(string[] connectedSteamIds)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			string environmentVariable = Environment.GetEnvironmentVariable("PUCK_SERVER_TOKEN");
			string environmentVariable2 = Environment.GetEnvironmentVariable("PUCK_SERVER_LAUNCHED_BY_STEAM_ID");
			MonoBehaviourSingleton<WebSocketManager>.Instance.Emit("serverAuthenticateRequest", new Dictionary<string, object>
			{
				{ "port", Server.Port },
				{ "pingPort", Server.PingPort },
				{
					"name",
					Server.Name.ToString()
				},
				{ "maxPlayers", Server.MaxPlayers },
				{
					"password",
					Server.Password.ToString()
				},
				{ "isPublic", Server.IsPublic },
				{ "isDedicated", Server.IsDedicated },
				{ "isHosted", Server.IsHosted },
				{
					"ownerSteamId",
					Server.OwnerSteamId.ToString()
				},
				{ "token", environmentVariable },
				{ "requestId", EdgegapManager.RequestId },
				{ "launchedBySteamId", environmentVariable2 },
				{ "connectedSteamIds", connectedSteamIds }
			}, "serverAuthenticateResponse");
		}
	}

	public void Server_Unauthenticate()
	{
		MonoBehaviourSingleton<WebSocketManager>.Instance.Emit("serverUnauthenticate");
	}

	public void Server_UpdateConnectedSteamIds(string[] steamIds)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			MonoBehaviourSingleton<WebSocketManager>.Instance.Emit("serverUpdateConnectedSteamIds", new Dictionary<string, object> { { "connectedSteamIds", steamIds } });
		}
	}

	private void Server_ConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
	{
		ulong clientNetworkId = request.ClientNetworkId;
		if (clientNetworkId == 0L)
		{
			response.Approved = true;
			Debug.Log($"[ServerManager] Host connection approved for {clientNetworkId}");
		}
		else
		{
			ConnectionData connectionData = JsonSerializer.Deserialize<ConnectionData>(Encoding.ASCII.GetString(request.Payload));
			Debug.Log($"[ServerManager] Connection approval incoming from {clientNetworkId} ({connectionData.SteamId})");
			string text = Server.Password.ToString();
			bool flag = !string.IsNullOrEmpty(text);
			Server_VerifyTimeouts();
			bool flag2 = !string.IsNullOrEmpty(connectionData.SocketId);
			bool flag3 = !string.IsNullOrEmpty(connectionData.SteamId);
			bool flag4 = NetworkManager.Singleton.ConnectedClientsList.Count >= Server.MaxPlayers;
			bool flag5 = flag3 && SteamIdTimeouts.ContainsKey(connectionData.SteamId);
			bool flag6 = flag3 && BannedSteamIds.Contains(connectionData.SteamId);
			bool flag7 = string.IsNullOrEmpty(connectionData.Password) && flag;
			bool flag8 = connectionData.Password == text || !flag;
			bool flag9 = ServerConfigurationManager.ClientRequiredModIds.Any((ulong modId) => !connectionData.EnabledModIds.Contains(modId));
			response.Approved = flag2 && flag3 && !flag4 && !flag5 && !flag6 && !flag7 && flag8 && !flag9;
			if (response.Approved)
			{
				if (ServerConfigurationManager.ServerConfiguration.usePuckBannedSteamIds)
				{
					response.Pending = true;
					if (ConnectionApprovalRequests.ContainsKey(connectionData.SteamId))
					{
						ConnectionApprovalRequests.Remove(connectionData.SteamId);
					}
					ConnectionApprovalRequests.Add(connectionData.SteamId, response);
					MonoBehaviourSingleton<WebSocketManager>.Instance.Emit("serverConnectionApprovalRequest", new Dictionary<string, object>
					{
						{ "steamId", connectionData.SteamId },
						{ "socketId", connectionData.SocketId }
					}, "serverConnectionApprovalResponse");
					Debug.Log($"[ServerManager] Connection approval request sent for {clientNetworkId} ({connectionData.SteamId})");
				}
				else
				{
					Debug.Log($"[ServerManager] Connection approved for {clientNetworkId} ({connectionData.SteamId})");
				}
			}
			else
			{
				ConnectionRejectionCode code = ConnectionRejectionCode.Unreachable;
				ulong[] clientRequiredModIds = ServerConfigurationManager.ClientRequiredModIds;
				if (!flag2)
				{
					code = ConnectionRejectionCode.InvalidSocketId;
				}
				else if (!flag3)
				{
					code = ConnectionRejectionCode.InvalidSteamId;
				}
				else if (flag4)
				{
					code = ConnectionRejectionCode.ServerFull;
				}
				else if (flag5)
				{
					code = ConnectionRejectionCode.TimedOut;
				}
				else if (flag6)
				{
					code = ConnectionRejectionCode.Banned;
				}
				else if (flag7)
				{
					code = ConnectionRejectionCode.MissingPassword;
				}
				else if (!flag8)
				{
					code = ConnectionRejectionCode.InvalidPassword;
				}
				else if (flag9)
				{
					code = ConnectionRejectionCode.MissingMods;
				}
				string reason = JsonSerializer.Serialize(new ConnectionRejection
				{
					code = code,
					clientRequiredModIds = clientRequiredModIds
				}, new JsonSerializerOptions
				{
					WriteIndented = true
				});
				response.Reason = reason;
				Debug.Log($"[ServerManager] Connection rejected for {clientNetworkId} ({connectionData.SteamId}): {Utils.GetConnectionRejectionMessage(code)}");
			}
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_ConnectionApproval", new Dictionary<string, object>
		{
			{ "clientId", clientNetworkId },
			{ "approved", response.Approved }
		});
	}

	public void Server_StartMetricsCoroutine()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Server_StopMetricsCoroutine();
			serverMetricsCoroutine = IMetrics(10f);
			StartCoroutine(serverMetricsCoroutine);
		}
	}

	public void Server_StopMetricsCoroutine()
	{
		if (serverMetricsCoroutine != null)
		{
			StopCoroutine(serverMetricsCoroutine);
		}
	}

	private IEnumerator IMetrics(float delay)
	{
		yield return new WaitForSeconds(delay);
		if (deltaTimeBuffer.Count <= 0)
		{
			deltaTimeBuffer.Clear();
			yield return null;
		}
		Debug.Log($"[ServerManager] FPS: {1f / Time.deltaTime} (min: {1f / deltaTimeBuffer.Max()}, average: {1f / deltaTimeBuffer.Average()}, max: {1f / deltaTimeBuffer.Min()})");
		deltaTimeBuffer.Clear();
		Server_StartMetricsCoroutine();
	}

	public void Server_StartBannedSteamIdsReloadCoroutine()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Server_StopBannedSteamIdsReloadCoroutine();
			bannedSteamIdsReloadCoroutine = IBannedSteamIdsReload(300f);
			StartCoroutine(bannedSteamIdsReloadCoroutine);
		}
	}

	public void Server_StopBannedSteamIdsReloadCoroutine()
	{
		if (bannedSteamIdsReloadCoroutine != null)
		{
			StopCoroutine(bannedSteamIdsReloadCoroutine);
		}
	}

	private IEnumerator IBannedSteamIdsReload(float delay)
	{
		yield return new WaitForSeconds(delay);
		LoadBannedSteamIds();
		Server_StartBannedSteamIdsReloadCoroutine();
	}

	[Rpc(SendTo.SpecifiedInParams)]
	public void Server_ServerConfigurationRpc(Server server, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				FastBufferWriter bufferWriter = __beginSendRpc(3940886306u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in server, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3940886306u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Server = server;
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnServerConfiguration", new Dictionary<string, object> { { "server", server } });
			}
		}
	}

	public void Server_TimeoutSteamId(string steamId)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			if (SteamIdTimeouts.ContainsKey(steamId))
			{
				SteamIdTimeouts.Remove(steamId);
			}
			SteamIdTimeouts.Add(steamId, Time.time);
		}
	}

	public void Server_VerifyTimeouts()
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return;
		}
		foreach (string item in SteamIdTimeouts.Keys.ToList())
		{
			if (Time.time - SteamIdTimeouts[item] > ServerConfigurationManager.ServerConfiguration.kickTimeout)
			{
				SteamIdTimeouts.Remove(item);
			}
		}
	}

	public void Server_KickPlayer(Player player, DisconnectionCode disconnectionCode = DisconnectionCode.Kicked, bool applyTimeout = true)
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return;
		}
		string steamId = player.SteamId.Value.ToString();
		if (player.OwnerClientId == 0L)
		{
			NetworkManager.Singleton.Shutdown(discardMessageQueue: true);
			return;
		}
		if (applyTimeout)
		{
			Server_TimeoutSteamId(steamId);
		}
		string reason = JsonSerializer.Serialize(new Disconnection
		{
			code = disconnectionCode
		});
		NetworkManager.Singleton.DisconnectClient(player.OwnerClientId, reason);
	}

	public void Server_BanPlayer(Player player)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			string steamId = player.SteamId.Value.ToString();
			Server_BanSteamId(steamId);
			Server_KickPlayer(player, DisconnectionCode.Banned, applyTimeout: false);
		}
	}

	public void Server_BanSteamId(string steamId)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			AddBannedSteamId(steamId);
		}
	}

	public void Server_UnbanSteamId(string steamId)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			RemoveBannedSteamId(steamId);
		}
	}

	public void Client_StartHost(ushort port, string name, int maxPlayers, string password, bool voip, bool isPublic, string ownerSteamId, bool uPnP = false)
	{
		if (!NetworkManager.Singleton.IsClient)
		{
			ServerConfiguration serverConfiguration = new ServerConfiguration();
			serverConfiguration.port = port;
			serverConfiguration.pingPort = (ushort)(port + 1);
			serverConfiguration.name = name;
			serverConfiguration.maxPlayers = maxPlayers;
			serverConfiguration.password = password;
			serverConfiguration.voip = voip;
			serverConfiguration.isPublic = isPublic;
			serverConfiguration.adminSteamIds = new string[1] { ownerSteamId };
			serverConfiguration.serverTickRate = 240;
			serverConfiguration.clientTickRate = 240;
			ServerConfiguration serverConfiguration2 = serverConfiguration;
			ServerConfigurationManager.ServerConfiguration = serverConfiguration2;
			Debug.Log("[ServerManager] Starting " + (isPublic ? "public" : "private") + " Puck host (" + Application.version + ")");
			UnityTransport.SetConnectionData("0.0.0.0", ServerConfigurationManager.ServerConfiguration.port);
			Server = new Server
			{
				Port = ServerConfigurationManager.ServerConfiguration.port,
				PingPort = ServerConfigurationManager.ServerConfiguration.pingPort,
				Name = ServerConfigurationManager.ServerConfiguration.name,
				MaxPlayers = ServerConfigurationManager.ServerConfiguration.maxPlayers,
				Password = ServerConfigurationManager.ServerConfiguration.password,
				Voip = ServerConfigurationManager.ServerConfiguration.voip,
				IsPublic = ServerConfigurationManager.ServerConfiguration.isPublic,
				IsDedicated = false,
				IsHosted = true,
				OwnerSteamId = ownerSteamId,
				SleepTimeout = ServerConfigurationManager.ServerConfiguration.sleepTimeout,
				ClientTickRate = ServerConfigurationManager.ServerConfiguration.clientTickRate,
				ClientRequiredModIds = ServerConfigurationManager.ClientRequiredModIds
			};
			if (uPnP)
			{
				Debug.Log($"[ServerManager] Forwarding port {Server.Port} & {Server.PingPort} with uPnP");
				uPnPHelper.Start(uPnPHelper.Protocol.UDP, Server.Port, 0, "Puck Port");
				Debug.Log(uPnPHelper.GetDebugMessages());
				Debug.Log(uPnPHelper.GetErrorMessages());
				uPnPHelper.Start(uPnPHelper.Protocol.UDP, Server.PingPort, 0, "Puck Ping Port");
				Debug.Log(uPnPHelper.GetDebugMessages());
				Debug.Log(uPnPHelper.GetErrorMessages());
			}
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnBeforeServerStarted", new Dictionary<string, object> { { "serverConfiguration", ServerConfigurationManager.ServerConfiguration } });
			IsHostStartInProgress = true;
		}
	}

	public void Client_StartServer(ushort port, bool uPnP = false)
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			Debug.Log("[ServerManager] Starting Puck server (" + Application.version + ")");
			UnityTransport.SetConnectionData("0.0.0.0", port);
			Server = new Server
			{
				Port = (EdgegapManager.IsEdgegap ? EdgegapManager.ArbitriumPortGamePortExternal : ServerConfigurationManager.ServerConfiguration.port),
				PingPort = (EdgegapManager.IsEdgegap ? EdgegapManager.ArbitriumPortPingPortExternal : ServerConfigurationManager.ServerConfiguration.pingPort),
				Name = ServerConfigurationManager.ServerConfiguration.name,
				MaxPlayers = ServerConfigurationManager.ServerConfiguration.maxPlayers,
				Password = ServerConfigurationManager.ServerConfiguration.password,
				Voip = ServerConfigurationManager.ServerConfiguration.voip,
				IsPublic = ServerConfigurationManager.ServerConfiguration.isPublic,
				IsDedicated = true,
				IsHosted = false,
				SleepTimeout = ServerConfigurationManager.ServerConfiguration.sleepTimeout,
				ClientTickRate = ServerConfigurationManager.ServerConfiguration.clientTickRate,
				ClientRequiredModIds = ServerConfigurationManager.ClientRequiredModIds
			};
			if (uPnP)
			{
				Debug.Log($"[ServerManager] Forwarding port {Server.Port} & {Server.PingPort} with uPnP");
				uPnPHelper.Start(uPnPHelper.Protocol.UDP, Server.Port, 0, "Puck Port");
				Debug.Log(uPnPHelper.GetDebugMessages());
				Debug.Log(uPnPHelper.GetErrorMessages());
				uPnPHelper.Start(uPnPHelper.Protocol.UDP, Server.PingPort, 0, "Puck Ping Port");
				Debug.Log(uPnPHelper.GetDebugMessages());
				Debug.Log(uPnPHelper.GetErrorMessages());
			}
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnBeforeServerStarted", new Dictionary<string, object> { { "serverConfiguration", ServerConfigurationManager.ServerConfiguration } });
			IsServerStartInProgress = true;
		}
	}

	private void OnApplicationQuit()
	{
		uPnPHelper.CloseAll();
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(3940886306u, __rpc_handler_3940886306, "Server_ServerConfigurationRpc");
		base.__initializeRpcs();
	}

	private static void __rpc_handler_3940886306(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out Server value, default(FastBufferWriter.ForNetworkSerializable));
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((ServerManager)target).Server_ServerConfigurationRpc(value, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected internal override string __getTypeName()
	{
		return "ServerManager";
	}
}
