using System.Collections.Generic;
using SocketIOClient;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

public class SteamIntegrationManagerController : MonoBehaviour
{
	private SteamIntegrationManager steamIntegrationManager;

	private void Awake()
	{
		steamIntegrationManager = GetComponent<SteamIntegrationManager>();
	}

	private void Start()
	{
		if (!Application.isBatchMode)
		{
			MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
			MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
			MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
			MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnGotAuthTicketForWebApi", Event_Client_OnGotAuthTicketForWebApi);
			MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerDataReady", Event_Client_OnPlayerDataReady);
			MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnScoreboardClickPlayer", Event_Client_OnScoreboardClickPlayer);
			MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnServerConfiguration", Event_Client_OnServerConfiguration);
			MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnModsClickFindMods", Event_Client_OnModsClickFindMods);
			MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("connect", WebSocket_Event_OnConnect);
			MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("playerAuthenticateResponse", WebSocket_Event_OnPlayerAuthenticateResponse);
		}
	}

	private void OnDestroy()
	{
		if (!Application.isBatchMode)
		{
			MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
			MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
			MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
			MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnGotAuthTicketForWebApi", Event_Client_OnGotAuthTicketForWebApi);
			MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerDataReady", Event_Client_OnPlayerDataReady);
			MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnScoreboardClickPlayer", Event_Client_OnScoreboardClickPlayer);
			MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnServerConfiguration", Event_Client_OnServerConfiguration);
			MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnModsClickFindMods", Event_Client_OnModsClickFindMods);
			MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("connect", WebSocket_Event_OnConnect);
			MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("playerAuthenticateResponse", WebSocket_Event_OnPlayerAuthenticateResponse);
		}
	}

	private void Event_OnClientDisconnected(Dictionary<string, object> message)
	{
		ulong num = (ulong)message["clientId"];
		if (NetworkManager.Singleton.LocalClientId == num)
		{
			steamIntegrationManager.SetRichPresenceMainMenu();
		}
	}

	private void Event_OnGamePhaseChanged(Dictionary<string, object> message)
	{
		GameState gameState = (GameState)message["gameState"];
		steamIntegrationManager.UpdateRichPresencePhase(gameState.Phase);
		steamIntegrationManager.UpdateRichPresenceScore(gameState.Phase != GamePhase.Warmup, gameState.Period, gameState.BlueScore, gameState.RedScore);
	}

	private void Event_OnPlayerRoleChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		Server server = NetworkBehaviourSingleton<ServerManager>.Instance.Server;
		GameState value = NetworkBehaviourSingleton<GameManager>.Instance.GameState.Value;
		if (player.IsLocalPlayer)
		{
			PlayerTeam value2 = player.Team.Value;
			if ((uint)(value2 - 2) <= 1u)
			{
				steamIntegrationManager.UpdateRichPresencePhase(value.Phase);
				steamIntegrationManager.UpdateRichPresenceTeam(player.Team.Value);
				steamIntegrationManager.UpdateRichPresenceRole(player.Role.Value);
				steamIntegrationManager.UpdateRichPresenceScore(value.Phase != GamePhase.Warmup, value.Period, value.BlueScore, value.RedScore);
				steamIntegrationManager.SetRichPresencePlaying(server, NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers().Count);
			}
			else
			{
				steamIntegrationManager.UpdateRichPresenceScore(value.Phase != GamePhase.Warmup, value.Period, value.BlueScore, value.RedScore);
				steamIntegrationManager.SetRichPresenceSpectating(server, NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers().Count);
			}
		}
	}

	private void Event_Client_OnGotAuthTicketForWebApi(Dictionary<string, object> message)
	{
		string value = (string)message["ticket"];
		MonoBehaviourSingleton<WebSocketManager>.Instance.Emit("playerAuthenticateRequest", new Dictionary<string, object> { { "ticket", value } }, "playerAuthenticateResponse");
	}

	private void Event_Client_OnPlayerDataReady(Dictionary<string, object> message)
	{
		steamIntegrationManager.GetLaunchCommandLine();
	}

	private void Event_Client_OnScoreboardClickPlayer(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		SteamFriends.ActivateGameOverlayToUser("steamID", new CSteamID(ulong.Parse(player.SteamId.Value.ToString())));
	}

	private void Event_Client_OnServerConfiguration(Dictionary<string, object> message)
	{
		Server server = (Server)message["server"];
		steamIntegrationManager.UpdateRichPresenceScore(show: false, 0, 0, 0);
		steamIntegrationManager.SetRichPresenceSpectating(server, 1);
	}

	private void Event_Client_OnModsClickFindMods(Dictionary<string, object> message)
	{
		SteamFriends.ActivateGameOverlayToWebPage("https://steamcommunity.com/app/2994020/workshop/");
	}

	private void WebSocket_Event_OnConnect(Dictionary<string, object> message)
	{
		steamIntegrationManager.GetAuthTicketForWebApi();
	}

	private void WebSocket_Event_OnPlayerAuthenticateResponse(Dictionary<string, object> message)
	{
		if (((SocketIOResponse)message["response"]).GetValue<PlayerAuthenticateResponse>().success)
		{
			steamIntegrationManager.SetRichPresenceMainMenu();
		}
	}
}
