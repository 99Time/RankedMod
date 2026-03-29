using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;

public class UIChatController : NetworkBehaviour
{
	private UIChat uiChat;

	private void Awake()
	{
		uiChat = GetComponent<UIChat>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerDespawned", Event_OnPlayerDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGoalScored", Event_OnGoalScored);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGameOver", Event_OnGameOver);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnShowGameUserInterfaceChanged", Event_Client_OnShowGameUserInterfaceChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnChatOpacityChanged", Event_Client_OnChatOpacityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnChatScaleChanged", Event_Client_OnChatScaleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnSynchronizeComplete", Event_Server_OnSynchronizeComplete);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnPlayerSubscription", Event_Server_OnPlayerSubscription);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnVoteStarted", Event_Server_OnVoteStarted);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnVoteProgress", Event_Server_OnVoteProgress);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnVoteSuccess", Event_Server_OnVoteSuccess);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnVoteFailed", Event_Server_OnVoteFailed);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnChatCommand", Event_Server_OnChatCommand);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnPlayerStateDelayed", Event_Server_OnPlayerStateDelayed);
		uiChat.Blur();
		uiChat.ClearChatMessages();
	}

	public override void OnNetworkDespawn()
	{
		uiChat.ClearChatMessages();
		base.OnNetworkDespawn();
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerDespawned", Event_OnPlayerDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGoalScored", Event_OnGoalScored);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGameOver", Event_OnGameOver);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnShowGameUserInterfaceChanged", Event_Client_OnShowGameUserInterfaceChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnChatOpacityChanged", Event_Client_OnChatOpacityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnChatScaleChanged", Event_Client_OnChatScaleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnSynchronizeComplete", Event_Server_OnSynchronizeComplete);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnPlayerSubscription", Event_Server_OnPlayerSubscription);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnVoteStarted", Event_Server_OnVoteStarted);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnVoteProgress", Event_Server_OnVoteProgress);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnVoteSuccess", Event_Server_OnVoteSuccess);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnVoteFailed", Event_Server_OnVoteFailed);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnChatCommand", Event_Server_OnChatCommand);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnPlayerStateDelayed", Event_Server_OnPlayerStateDelayed);
		base.OnDestroy();
	}

	private void Event_Client_OnShowGameUserInterfaceChanged(Dictionary<string, object> message)
	{
		if (NetworkBehaviourSingleton<UIManager>.Instance.UIState != UIState.MainMenu)
		{
			if ((bool)message["value"])
			{
				uiChat.Show();
			}
			else
			{
				uiChat.Hide();
			}
		}
	}

	private void Event_Client_OnChatOpacityChanged(Dictionary<string, object> message)
	{
		float opacity = (float)message["value"];
		uiChat.SetOpacity(opacity);
	}

	private void Event_Client_OnChatScaleChanged(Dictionary<string, object> message)
	{
		float scale = (float)message["value"];
		uiChat.SetScale(scale);
	}

	private void Event_OnPlayerDespawned(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (!base.NetworkManager.ShutdownInProgress && NetworkManager.Singleton.IsServer && !player.IsReplay.Value)
		{
			uiChat.Server_SendSystemChatMessage($"<b>{player.Username.Value}</b> has left the server.");
		}
	}

	private void Event_OnPlayerTeamChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		PlayerTeam playerTeam = (PlayerTeam)message["newTeam"];
		if (NetworkManager.Singleton.IsServer && !player.IsReplay.Value)
		{
			string text = playerTeam switch
			{
				PlayerTeam.Blue => uiChat.WrapInTeamColor(playerTeam, "BLUE"), 
				PlayerTeam.Red => uiChat.WrapInTeamColor(playerTeam, "RED"), 
				PlayerTeam.Spectator => uiChat.WrapInTeamColor(playerTeam, "SPECTATOR"), 
				_ => null, 
			};
			if (text != null)
			{
				uiChat.Server_SendSystemChatMessage(uiChat.WrapPlayerUsername(player) + " joined team " + text + ".");
			}
		}
	}

	private void Event_OnGoalScored(Dictionary<string, object> message)
	{
		bool flag = (bool)message["hasGoalPlayer"];
		ulong clientId = (ulong)message["goalPlayerClientId"];
		_ = (bool)message["hasAssistPlayer"];
		_ = (ulong)message["assistPlayerClientId"];
		float num = (float)message["speedAcrossLine"];
		float num2 = (float)message["highestSpeedSinceStick"];
		string text = num.ToString().Replace(",", ".");
		string text2 = num2.ToString().Replace(",", ".");
		if (!NetworkManager.Singleton.IsServer || !NetworkBehaviourSingleton<PuckManager>.Instance.GetPuck())
		{
			return;
		}
		if (flag)
		{
			Player playerByClientId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId);
			if ((bool)playerByClientId)
			{
				uiChat.Server_SendSystemChatMessage(uiChat.WrapPlayerUsername(playerByClientId) + " scored a goal, <b><united>" + text + "</united> &units</b> across line, <b><united>" + text2 + "</united> &units</b> from stick.");
			}
		}
		else
		{
			uiChat.Server_SendSystemChatMessage("Goal scored, <b><united>" + text + "</united> &units</b> across line, <b><united>" + text2 + "</united> &units</b> from stick.");
		}
	}

	private void Event_OnGameOver(Dictionary<string, object> message)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			PlayerTeam playerTeam = (PlayerTeam)message["winningTeam"];
			int num = (int)message["blueScore"];
			int num2 = (int)message["redScore"];
			string text = playerTeam switch
			{
				PlayerTeam.Blue => uiChat.WrapInTeamColor(playerTeam, "BLUE"), 
				PlayerTeam.Red => uiChat.WrapInTeamColor(playerTeam, "RED"), 
				_ => null, 
			};
			if (text != null)
			{
				uiChat.Server_SendSystemChatMessage(text + " wins the game with a score " + uiChat.WrapInTeamColor(PlayerTeam.Blue, num.ToString()) + "-" + uiChat.WrapInTeamColor(PlayerTeam.Red, num2.ToString()) + ".");
			}
		}
	}

	private void Event_Server_OnSynchronizeComplete(Dictionary<string, object> message)
	{
		ulong clientId = (ulong)message["clientId"];
		uiChat.Server_SendSystemChatMessage("Welcome to Puck! Use the <b>/help</b> command to display available server chat commands.", clientId);
	}

	private void Event_Server_OnPlayerSubscription(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (!player.IsReplay.Value)
		{
			uiChat.Server_SendSystemChatMessage(uiChat.WrapPlayerUsername(player) + " has joined the server!");
		}
	}

	private void Event_Server_OnVoteStarted(Dictionary<string, object> message)
	{
		Vote vote = (Vote)message["vote"];
		Player startedBy = vote.StartedBy;
		if (!startedBy)
		{
			return;
		}
		switch (vote.Type)
		{
		case VoteType.Start:
			uiChat.Server_SendSystemChatMessage($"{uiChat.WrapPlayerUsername(startedBy)} has started a vote to start a new game. (1/{vote.VotesNeeded})");
			break;
		case VoteType.Warmup:
			uiChat.Server_SendSystemChatMessage($"{uiChat.WrapPlayerUsername(startedBy)} has started a vote to enter warmup. (1/{vote.VotesNeeded})");
			break;
		case VoteType.Kick:
		{
			FixedString32Bytes steamId = (FixedString32Bytes)vote.Data;
			Player playerBySteamId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(steamId);
			if ((bool)playerBySteamId)
			{
				uiChat.Server_SendSystemChatMessage($"{uiChat.WrapPlayerUsername(startedBy)} has started a vote to kick #{playerBySteamId.Number.Value} {playerBySteamId.Username.Value}. (1/{vote.VotesNeeded})");
			}
			break;
		}
		}
	}

	private void Event_Server_OnVoteProgress(Dictionary<string, object> message)
	{
		Vote vote = (Vote)message["vote"];
		Player player = (Player)message["voter"];
		if (!player)
		{
			return;
		}
		switch (vote.Type)
		{
		case VoteType.Start:
			uiChat.Server_SendSystemChatMessage($"{uiChat.WrapPlayerUsername(player)} voted to start a new game. ({vote.Votes}/{vote.VotesNeeded})");
			break;
		case VoteType.Warmup:
			uiChat.Server_SendSystemChatMessage($"{uiChat.WrapPlayerUsername(player)} voted to enter warmup. ({vote.Votes}/{vote.VotesNeeded})");
			break;
		case VoteType.Kick:
		{
			FixedString32Bytes steamId = (FixedString32Bytes)vote.Data;
			Player playerBySteamId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(steamId);
			if ((bool)playerBySteamId)
			{
				uiChat.Server_SendSystemChatMessage($"{uiChat.WrapPlayerUsername(player)} voted to kick #{playerBySteamId.Number.Value} {playerBySteamId.Username.Value}. ({vote.Votes}/{vote.VotesNeeded})");
			}
			break;
		}
		}
	}

	private void Event_Server_OnVoteSuccess(Dictionary<string, object> message)
	{
		Vote vote = (Vote)message["vote"];
		switch (vote.Type)
		{
		case VoteType.Start:
			uiChat.Server_SendSystemChatMessage("Vote passed - starting a new game!");
			break;
		case VoteType.Warmup:
			uiChat.Server_SendSystemChatMessage("Vote passed - entering warmup!");
			break;
		case VoteType.Kick:
		{
			FixedString32Bytes steamId = (FixedString32Bytes)vote.Data;
			Player playerBySteamId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(steamId);
			if ((bool)playerBySteamId)
			{
				uiChat.Server_SendSystemChatMessage($"Vote passed - kicking #{playerBySteamId.Number.Value} {playerBySteamId.Username.Value}!");
			}
			break;
		}
		}
	}

	private void Event_Server_OnVoteFailed(Dictionary<string, object> message)
	{
		_ = ((Vote)message["vote"]).Type;
		uiChat.Server_SendSystemChatMessage("Vote failed - timed out.");
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
		if (text == "/help")
		{
			string text2 = "";
			if (flag)
			{
				text2 = "\nAdmin commands:\n* <b>/kick [name/number]</b> - Kick a player\n* <b>/ban [name/number]</b> - Ban a player\n* <b>/bansteamid [Steam ID]</b> - Ban a Steam ID\n* <b>/unbansteamid [Steam ID]</b> - Unban a Steam ID\n* <b>/pause</b> - Pause the game\n* <b>/resume</b> - Resume the game";
			}
			uiChat.Server_SendSystemChatMessage("Server commands:\n* <b>/help</b> - Displays this message\n* <b>/votestart</b>(/vs) - Cast a vote to start a new game\n* <b>/votewarmup</b>(/vw) - Cast a vote to enter warmup\n* <b>/votekick [name/number]</b>(/vk) - Cast a vote to kick a player" + text2, clientId);
		}
	}

	private void Event_Server_OnPlayerStateDelayed(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		PlayerState playerState = (PlayerState)message["newState"];
		PlayerState playerState2 = (PlayerState)message["oldState"];
		float num = (float)message["delay"];
		if ((bool)player && (playerState2 == PlayerState.PositionSelectBlue || playerState2 == PlayerState.PositionSelectRed) && playerState == PlayerState.Play)
		{
			uiChat.Server_SendSystemChatMessage($"Joining the match in {num} seconds...", player.OwnerClientId);
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
		return "UIChatController";
	}
}
