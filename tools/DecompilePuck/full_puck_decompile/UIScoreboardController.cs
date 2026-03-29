using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

internal class UIScoreboardController : NetworkBehaviour
{
	private UIScoreboard uiScoreboard;

	private void Awake()
	{
		uiScoreboard = GetComponent<UIScoreboard>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerDespawned", Event_OnPlayerDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerAdded", Event_OnPlayerAdded);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerRemoved", Event_OnPlayerRemoved);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerUsernameChanged", Event_OnPlayerUsernameChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerGoalsChanged", Event_OnPlayerGoalsChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerAssistsChanged", Event_OnPlayerAssistsChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerPingChanged", Event_OnPlayerPingChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerPositionChanged", Event_OnPlayerPositionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerPatreonLevelChanged", Event_OnPlayerPatreonLevelChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerAdminLevelChanged", Event_OnPlayerAdminLevelChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerSteamIdChanged", Event_OnPlayerSteamIdChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnServerConfiguration", Event_Client_OnServerConfiguration);
	}

	public override void OnNetworkDespawn()
	{
		uiScoreboard.Clear();
		base.OnNetworkDespawn();
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerDespawned", Event_OnPlayerDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerAdded", Event_OnPlayerAdded);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerRemoved", Event_OnPlayerRemoved);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerUsernameChanged", Event_OnPlayerUsernameChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerGoalsChanged", Event_OnPlayerGoalsChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerAssistsChanged", Event_OnPlayerAssistsChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerPingChanged", Event_OnPlayerPingChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerPositionChanged", Event_OnPlayerPositionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerPatreonLevelChanged", Event_OnPlayerPatreonLevelChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerAdminLevelChanged", Event_OnPlayerAdminLevelChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerSteamIdChanged", Event_OnPlayerSteamIdChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnServerConfiguration", Event_Client_OnServerConfiguration);
		base.OnDestroy();
	}

	private void Event_OnPlayerSpawned(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (!player.IsReplay.Value)
		{
			uiScoreboard.AddPlayer(player);
		}
	}

	private void Event_OnPlayerDespawned(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		uiScoreboard.RemovePlayer(player);
	}

	private void Event_OnPlayerAdded(Dictionary<string, object> message)
	{
		uiScoreboard.UpdateServer(NetworkBehaviourSingleton<ServerManager>.Instance.Server, NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers().Count);
	}

	private void Event_OnPlayerRemoved(Dictionary<string, object> message)
	{
		uiScoreboard.UpdateServer(NetworkBehaviourSingleton<ServerManager>.Instance.Server, NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers().Count);
	}

	private void Event_OnPlayerTeamChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		uiScoreboard.UpdatePlayer(player);
	}

	private void Event_OnPlayerUsernameChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		uiScoreboard.UpdatePlayer(player);
	}

	private void Event_OnPlayerGoalsChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		uiScoreboard.UpdatePlayer(player);
	}

	private void Event_OnPlayerAssistsChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		uiScoreboard.UpdatePlayer(player);
	}

	private void Event_OnPlayerPingChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		uiScoreboard.UpdatePlayer(player);
	}

	private void Event_OnPlayerPositionChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		uiScoreboard.UpdatePlayer(player);
	}

	private void Event_OnPlayerPatreonLevelChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		uiScoreboard.UpdatePlayer(player);
	}

	private void Event_OnPlayerAdminLevelChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		uiScoreboard.UpdatePlayer(player);
	}

	private void Event_OnPlayerSteamIdChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		uiScoreboard.UpdatePlayer(player);
	}

	private void Event_OnGamePhaseChanged(Dictionary<string, object> message)
	{
		GamePhase gamePhase = (GamePhase)message["newGamePhase"];
		int num = (int)message["period"];
		if (gamePhase == GamePhase.Playing || gamePhase == GamePhase.GameOver)
		{
			string data = "";
			NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers().ForEach(delegate(Player player)
			{
				data += $"\nPlayer {player.Username.Value} ({player.OwnerClientId}) [{player.SteamId.Value}] has {player.Goals.Value} goals and {player.Assists.Value} assists";
			});
			string text = ((gamePhase == GamePhase.Playing) ? $"Period {num}" : "Game over");
			Debug.Log("[UIScoreboardController] " + text + ": " + data);
		}
	}

	private void Event_Client_OnServerConfiguration(Dictionary<string, object> message)
	{
		Server server = (Server)message["server"];
		uiScoreboard.UpdateServer(server, NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers().Count);
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
		return "UIScoreboardController";
	}
}
