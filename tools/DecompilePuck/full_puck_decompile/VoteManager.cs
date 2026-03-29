using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class VoteManager : NetworkBehaviourSingleton<VoteManager>
{
	private List<Vote> votes = new List<Vote>();

	private void Update()
	{
		if (!base.IsSpawned || !NetworkManager.Singleton.IsServer)
		{
			return;
		}
		foreach (Vote item in votes.ToList())
		{
			item.Tick(Time.deltaTime);
		}
	}

	public void Server_CreateVote(VoteType voteType, int votesNeeded, Player startedBy, object data = null)
	{
		if (NetworkManager.Singleton.IsServer && !Server_IsVoteStarted(voteType))
		{
			new Vote(voteType, votesNeeded, startedBy, OnVoteStarted, OnVoteProgress, OnVoteSuccess, OnVoteFailed, data);
		}
	}

	public void Server_SubmitVote(VoteType voteType, Player voter)
	{
		if (NetworkManager.Singleton.IsServer && Server_IsVoteStarted(voteType))
		{
			Server_GetVote(voteType).SubmitVote(voter);
		}
	}

	public bool Server_IsVoteStarted(VoteType type)
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return false;
		}
		return votes.Any((Vote v) => v.Type == type && v.IsInProgress);
	}

	public Vote Server_GetVote(VoteType type)
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return null;
		}
		if (!Server_IsVoteStarted(type))
		{
			return null;
		}
		return votes.FirstOrDefault((Vote v) => v.Type == type);
	}

	private void AddVote(Vote vote)
	{
		votes.Add(vote);
	}

	private void RemoveVote(Vote vote)
	{
		votes.Remove(vote);
	}

	private void OnVoteStarted(Vote vote)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnVoteStarted", new Dictionary<string, object> { { "vote", vote } });
		AddVote(vote);
	}

	private void OnVoteProgress(Vote vote, Player voter)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnVoteProgress", new Dictionary<string, object>
		{
			{ "vote", vote },
			{ "voter", voter }
		});
	}

	private void OnVoteSuccess(Vote vote)
	{
		RemoveVote(vote);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnVoteSuccess", new Dictionary<string, object> { { "vote", vote } });
	}

	private void OnVoteFailed(Vote vote)
	{
		RemoveVote(vote);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnVoteFailed", new Dictionary<string, object> { { "vote", vote } });
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
		return "VoteManager";
	}
}
