using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VoteManagerController : MonoBehaviour
{
	private VoteManager voteManager;

	private void Awake()
	{
		voteManager = GetComponent<VoteManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnChatCommand", Event_Server_OnChatCommand);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnChatCommand", Event_Server_OnChatCommand);
	}

	private void Event_Server_OnChatCommand(Dictionary<string, object> message)
	{
		ulong clientId = (ulong)message["clientId"];
		string text = (string)message["command"];
		string[] array = (string[])message["args"];
		Player playerByClientId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId);
		bool allowVoting = NetworkBehaviourSingleton<ServerManager>.Instance.ServerConfigurationManager.ServerConfiguration.allowVoting;
		switch (text)
		{
		case "/vs":
		case "/votestart":
			if (allowVoting)
			{
				if (voteManager.Server_IsVoteStarted(VoteType.Start))
				{
					voteManager.Server_SubmitVote(VoteType.Start, playerByClientId);
					break;
				}
				int votesNeeded2 = Mathf.RoundToInt((float)NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers().Count / 2f + 0.5f);
				voteManager.Server_CreateVote(VoteType.Start, votesNeeded2, playerByClientId);
				voteManager.Server_SubmitVote(VoteType.Start, playerByClientId);
			}
			break;
		case "/vw":
		case "/votewarmup":
			if (allowVoting)
			{
				if (voteManager.Server_IsVoteStarted(VoteType.Warmup))
				{
					voteManager.Server_SubmitVote(VoteType.Warmup, playerByClientId);
					break;
				}
				int votesNeeded3 = Mathf.RoundToInt((float)NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers().Count / 2f + 0.5f);
				voteManager.Server_CreateVote(VoteType.Warmup, votesNeeded3, playerByClientId);
				voteManager.Server_SubmitVote(VoteType.Warmup, playerByClientId);
			}
			break;
		case "/vk":
		case "/votekick":
			if (!allowVoting)
			{
				break;
			}
			if (voteManager.Server_IsVoteStarted(VoteType.Kick))
			{
				voteManager.Server_SubmitVote(VoteType.Kick, playerByClientId);
			}
			else
			{
				if (array.Length < 1)
				{
					break;
				}
				int votesNeeded = Mathf.RoundToInt((float)NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers().Count / 2f + 0.5f);
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
				if (!NetworkBehaviourSingleton<ServerManager>.Instance.AdminSteamIds.Contains(player.SteamId.Value.ToString()))
				{
					voteManager.Server_CreateVote(VoteType.Kick, votesNeeded, playerByClientId, player.SteamId.Value);
					voteManager.Server_SubmitVote(VoteType.Kick, playerByClientId);
				}
			}
			break;
		}
	}
}
