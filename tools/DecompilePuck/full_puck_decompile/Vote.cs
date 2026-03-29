using System;
using System.Collections.Generic;
using Unity.Collections;

public class Vote
{
	public VoteType Type;

	public List<FixedString32Bytes> VoterSteamIds = new List<FixedString32Bytes>();

	public int VotesNeeded;

	public float Timeout = 60f;

	public Player StartedBy;

	public bool IsInProgress;

	public object Data;

	private Action<Vote, Player> onVoteProgress;

	private Action<Vote> onVoteSuccess;

	private Action<Vote> onVoteFailed;

	public int Votes => VoterSteamIds.Count;

	public Vote(VoteType type, int votesNeeded, Player startedBy = null, Action<Vote> onVoteStarted = null, Action<Vote, Player> onVoteProgress = null, Action<Vote> onVoteSuccess = null, Action<Vote> onVoteFailed = null, object data = null)
	{
		Type = type;
		VotesNeeded = votesNeeded;
		StartedBy = startedBy;
		IsInProgress = true;
		this.onVoteProgress = onVoteProgress;
		this.onVoteSuccess = onVoteSuccess;
		this.onVoteFailed = onVoteFailed;
		Data = data;
		onVoteStarted?.Invoke(this);
		SubmitVote(startedBy, notifyListeners: false);
	}

	public void SubmitVote(Player voter, bool notifyListeners = true)
	{
		if (!VoterSteamIds.Contains(voter.SteamId.Value))
		{
			VoterSteamIds.Add(voter.SteamId.Value);
			if (notifyListeners)
			{
				onVoteProgress?.Invoke(this, voter);
			}
			if (VoterSteamIds.Count >= VotesNeeded)
			{
				IsInProgress = false;
				onVoteSuccess?.Invoke(this);
			}
		}
	}

	public void Tick(float deltaTime)
	{
		Timeout -= deltaTime;
		if (IsInProgress && Timeout <= 0f)
		{
			IsInProgress = false;
			onVoteFailed?.Invoke(this);
		}
	}
}
