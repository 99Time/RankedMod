using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
	private Player player;

	private IEnumerator pingIntervalCoroutine;

	private void Awake()
	{
		player = GetComponent<Player>();
	}

	public override void OnNetworkSpawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerPositionClaimedByChanged", Event_OnPlayerPositionClaimedByChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerPositionChanged", Event_OnPlayerPositionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGoalScored", Event_OnGoalScored);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnChatCommand", Event_Server_OnChatCommand);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnPlayerSubscription", Event_Server_OnPlayerSubscription);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerSelectTeam", Event_Client_OnPlayerSelectTeam);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnHandednessChanged", Event_Client_OnHandednessChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPauseMenuClickSwitchTeam", Event_Client_OnPauseMenuClickSwitchTeam);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerRequestPositionSelect", Event_Client_OnPlayerRequestPositionSelect);
		if (NetworkManager.Singleton.IsServer)
		{
			pingIntervalCoroutine = IPingInterval();
			StartCoroutine(pingIntervalCoroutine);
		}
		base.OnNetworkSpawn();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerPositionClaimedByChanged", Event_OnPlayerPositionClaimedByChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerPositionChanged", Event_OnPlayerPositionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGoalScored", Event_OnGoalScored);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnChatCommand", Event_Server_OnChatCommand);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnPlayerSubscription", Event_Server_OnPlayerSubscription);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerSelectTeam", Event_Client_OnPlayerSelectTeam);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnHandednessChanged", Event_Client_OnHandednessChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPauseMenuClickSwitchTeam", Event_Client_OnPauseMenuClickSwitchTeam);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerRequestPositionSelect", Event_Client_OnPlayerRequestPositionSelect);
		if (NetworkManager.Singleton.IsServer && pingIntervalCoroutine != null)
		{
			StopCoroutine(pingIntervalCoroutine);
		}
		base.OnNetworkDespawn();
	}

	private IEnumerator IPingInterval()
	{
		yield return new WaitForSeconds(10f);
		player.Server_UpdatePing();
		pingIntervalCoroutine = IPingInterval();
		StartCoroutine(pingIntervalCoroutine);
	}

	private void Event_OnPlayerTeamChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (NetworkManager.Singleton.IsServer && base.OwnerClientId == player.OwnerClientId)
		{
			Debug.Log($"[PlayerController] Player {player.Username.Value} ({player.OwnerClientId}) changed team to {player.Team.Value}");
			switch (player.Team.Value)
			{
			case PlayerTeam.Blue:
				player.Client_SetPlayerStateRpc(PlayerState.PositionSelectBlue);
				break;
			case PlayerTeam.Red:
				player.Client_SetPlayerStateRpc(PlayerState.PositionSelectRed);
				break;
			case PlayerTeam.Spectator:
				player.Client_SetPlayerStateRpc(PlayerState.Spectate);
				break;
			}
		}
	}

	private void Event_OnPlayerPositionClaimedByChanged(Dictionary<string, object> message)
	{
		PlayerPosition playerPosition = (PlayerPosition)message["playerPosition"];
		Player player = (Player)message["oldClaimedBy"];
		Player player2 = (Player)message["newClaimedBy"];
		if (NetworkManager.Singleton.IsServer)
		{
			if ((bool)player && player.OwnerClientId == base.OwnerClientId)
			{
				Debug.Log($"[PlayerController] Player {this.player.Username.Value} ({this.player.OwnerClientId}) was unassigned position {playerPosition.Name}");
				this.player.PlayerPositionReference.Value = default(NetworkObjectReference);
			}
			if ((bool)player2 && player2.OwnerClientId == base.OwnerClientId)
			{
				Debug.Log($"[PlayerController] Player {this.player.Username.Value} ({this.player.OwnerClientId}) was assigned {playerPosition.Name}");
				this.player.PlayerPositionReference.Value = new NetworkObjectReference(playerPosition.NetworkObject);
			}
		}
	}

	private void Event_OnPlayerPositionChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (!NetworkManager.Singleton.IsServer || base.OwnerClientId != player.OwnerClientId)
		{
			return;
		}
		if ((bool)player.PlayerPosition)
		{
			player.Role.Value = player.PlayerPosition.Role;
			switch (NetworkBehaviourSingleton<GameManager>.Instance.GameState.Value.Phase)
			{
			case GamePhase.Warmup:
			case GamePhase.FaceOff:
				player.Client_SetPlayerStateRpc(PlayerState.Play);
				break;
			case GamePhase.Playing:
				player.Client_SetPlayerStateRpc(PlayerState.Play, NetworkBehaviourSingleton<ServerManager>.Instance.ServerConfigurationManager.ServerConfiguration.joinMidMatchDelay);
				break;
			}
		}
		else
		{
			player.Role.Value = PlayerRole.None;
		}
	}

	public void Event_OnGoalScored(Dictionary<string, object> message)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			bool num = (bool)message["hasGoalPlayer"];
			ulong num2 = (ulong)message["goalPlayerClientId"];
			bool flag = (bool)message["hasAssistPlayer"];
			ulong num3 = (ulong)message["assistPlayerClientId"];
			bool flag2 = (bool)message["hasSecondAssistPlayer"];
			ulong num4 = (ulong)message["secondAssistPlayerClientId"];
			if (num && num2 == base.OwnerClientId)
			{
				player.Goals.Value++;
			}
			if (flag && num3 == base.OwnerClientId)
			{
				player.Assists.Value++;
			}
			if (flag2 && num4 == base.OwnerClientId)
			{
				player.Assists.Value++;
			}
		}
	}

	private void Event_OnGamePhaseChanged(Dictionary<string, object> message)
	{
		bool flag = (bool)message["isFirstFaceOff"];
		GamePhase gamePhase = (GamePhase)message["newGamePhase"];
		GamePhase gamePhase2 = (GamePhase)message["oldGamePhase"];
		if (!NetworkManager.Singleton.IsServer)
		{
			return;
		}
		if (flag)
		{
			player.Server_ResetPoints();
		}
		if (player.Team.Value == PlayerTeam.None || player.Team.Value == PlayerTeam.Spectator || !player.PlayerPosition)
		{
			return;
		}
		switch (gamePhase)
		{
		case GamePhase.Warmup:
			if (gamePhase2 == GamePhase.GameOver)
			{
				player.Client_SetPlayerStateRpc((player.Team.Value == PlayerTeam.Blue) ? PlayerState.PositionSelectBlue : PlayerState.PositionSelectRed);
			}
			else
			{
				player.Client_SetPlayerStateRpc(PlayerState.Play);
			}
			break;
		case GamePhase.FaceOff:
		case GamePhase.GameOver:
			player.Client_SetPlayerStateRpc(PlayerState.Play);
			break;
		case GamePhase.Replay:
			player.Client_SetPlayerStateRpc(PlayerState.Replay);
			break;
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
		bool flag = NetworkBehaviourSingleton<ServerManager>.Instance.AdminSteamIds.Contains(playerByClientId.SteamId.Value.ToString());
		if (!(text == "/username"))
		{
			if (text == "/number" && flag && array.Length >= 1 && int.TryParse(array[0], out var result))
			{
				playerByClientId.Client_SetPlayerNumberRpc(result);
			}
		}
		else if (flag && array.Length >= 1)
		{
			playerByClientId.Client_SetPlayerUsernameRpc(array[0]);
		}
	}

	private void Event_Server_OnPlayerSubscription(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			player.Client_SetPlayerStateRpc(PlayerState.TeamSelect);
		}
	}

	private void Event_Client_OnPlayerSelectTeam(Dictionary<string, object> message)
	{
		if (base.IsOwner)
		{
			PlayerTeam team = (PlayerTeam)message["team"];
			player.Client_SetPlayerTeamRpc(team);
		}
	}

	private void Event_Client_OnHandednessChanged(Dictionary<string, object> message)
	{
		if (base.IsOwner)
		{
			string text = (string)message["value"];
			player.Client_SetPlayerHandednessRpc((!(text == "LEFT")) ? PlayerHandedness.Right : PlayerHandedness.Left);
		}
	}

	private void Event_Client_OnPauseMenuClickSwitchTeam(Dictionary<string, object> message)
	{
		if (base.IsOwner)
		{
			player.Client_SetPlayerStateRpc(PlayerState.TeamSelect);
		}
	}

	private void Event_Client_OnPlayerRequestPositionSelect(Dictionary<string, object> message)
	{
		if (base.IsOwner)
		{
			player.Client_SetPlayerStateRpc((player.Team.Value == PlayerTeam.Blue) ? PlayerState.PositionSelectBlue : PlayerState.PositionSelectRed);
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
		return "PlayerController";
	}
}
