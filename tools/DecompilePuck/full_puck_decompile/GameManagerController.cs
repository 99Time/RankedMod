using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class GameManagerController : NetworkBehaviour
{
	private GameManager gameManager;

	private void Awake()
	{
		gameManager = GetComponent<GameManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnServerReady", Event_Server_OnServerReady);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnPuckEnterTeamGoal", Event_Server_OnPuckEnterTeamGoal);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnVoteSuccess", Event_Server_OnVoteSuccess);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnChatCommand", Event_Server_OnChatCommand);
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnServerReady", Event_Server_OnServerReady);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnPuckEnterTeamGoal", Event_Server_OnPuckEnterTeamGoal);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnVoteSuccess", Event_Server_OnVoteSuccess);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnChatCommand", Event_Server_OnChatCommand);
		base.OnDestroy();
	}

	private void Event_Server_OnServerReady(Dictionary<string, object> message)
	{
		gameManager.Server_StartGame();
		if (NetworkBehaviourSingleton<ServerManager>.Instance.ServerConfigurationManager.ServerConfiguration.startPaused)
		{
			gameManager.Server_StopGameStateTickCoroutine();
		}
		else
		{
			gameManager.Server_StartGameStateTickCoroutine();
		}
	}

	private void Event_Server_OnPuckEnterTeamGoal(Dictionary<string, object> message)
	{
		PlayerTeam playerTeam = (PlayerTeam)message["team"];
		Puck puck = (Puck)message["puck"];
		if (gameManager.Phase != GamePhase.Playing)
		{
			return;
		}
		PlayerTeam team = ((playerTeam == PlayerTeam.Blue) ? PlayerTeam.Red : PlayerTeam.Blue);
		List<KeyValuePair<Player, float>> playerCollisions = puck.GetPlayerCollisions();
		List<KeyValuePair<Player, float>> playerCollisionsByTeam = puck.GetPlayerCollisionsByTeam(team);
		Player lastPlayer = null;
		Player goalPlayer = null;
		Player assistPlayer = null;
		Player secondAssistPlayer = null;
		if (playerCollisionsByTeam.Count >= 1)
		{
			goalPlayer = playerCollisionsByTeam[playerCollisionsByTeam.Count - 1].Key;
			if (playerCollisionsByTeam.Count > 1)
			{
				assistPlayer = playerCollisionsByTeam[playerCollisionsByTeam.Count - 2].Key;
			}
			if (playerCollisionsByTeam.Count > 2)
			{
				secondAssistPlayer = playerCollisionsByTeam[playerCollisionsByTeam.Count - 3].Key;
			}
		}
		if (playerCollisions.Count >= 1)
		{
			lastPlayer = playerCollisions[playerCollisions.Count - 1].Key;
		}
		gameManager.Server_GoalScored(team, lastPlayer, goalPlayer, assistPlayer, secondAssistPlayer, puck);
	}

	private void Event_Server_OnVoteSuccess(Dictionary<string, object> message)
	{
		Vote vote = (Vote)message["vote"];
		switch (vote.Type)
		{
		case VoteType.Start:
			Debug.Log($"[GameManagerController] Vote succeeded to start game ({vote.Votes}/{vote.VotesNeeded})");
			gameManager.Server_StartGame(warmup: false, 10);
			break;
		case VoteType.Warmup:
			Debug.Log($"[GameManagerController] Vote succeeded to start warmup ({vote.Votes}/{vote.VotesNeeded})");
			gameManager.Server_StartGame();
			break;
		}
	}

	private void Event_Server_OnChatCommand(Dictionary<string, object> message)
	{
		ulong clientId = (ulong)message["clientId"];
		string text = (string)message["command"];
		_ = (string[])message["args"];
		Player playerByClientId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId);
		if (!playerByClientId)
		{
			return;
		}
		bool flag = NetworkBehaviourSingleton<ServerManager>.Instance.AdminSteamIds.Contains(playerByClientId.SteamId.Value.ToString());
		switch (text)
		{
		case "/start":
			if (flag)
			{
				NetworkBehaviourSingleton<UIChat>.Instance.Server_SendSystemChatMessage("<b><color=orange>ADMIN</color></b> started the game.");
				gameManager.Server_StartGame(warmup: false, 10);
			}
			break;
		case "/warmup":
			if (flag)
			{
				NetworkBehaviourSingleton<UIChat>.Instance.Server_SendSystemChatMessage("<b><color=orange>ADMIN</color></b> started warmup.");
				gameManager.Server_StartGame();
			}
			break;
		case "/pause":
			if (flag)
			{
				NetworkBehaviourSingleton<UIChat>.Instance.Server_SendSystemChatMessage("<b><color=orange>ADMIN</color></b> paused the game.");
				gameManager.Server_StopGameStateTickCoroutine();
			}
			break;
		case "/resume":
			if (flag)
			{
				NetworkBehaviourSingleton<UIChat>.Instance.Server_SendSystemChatMessage("<b><color=orange>ADMIN</color></b> resumed the game.");
				gameManager.Server_StartGameStateTickCoroutine();
			}
			break;
		case "/debug":
			if (flag)
			{
				if (gameManager.IsDebugGameStateCoroutineRunning)
				{
					gameManager.Server_StopDebugGameStateCoroutine();
				}
				else
				{
					gameManager.Server_StartDebugGameStateCoroutine(0.1f);
				}
			}
			break;
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
		return "GameManagerController";
	}
}
