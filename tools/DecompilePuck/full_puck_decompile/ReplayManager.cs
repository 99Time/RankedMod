using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ReplayManager : NetworkBehaviourSingleton<ReplayManager>
{
	[Header("Settings")]
	[SerializeField]
	private int tickRate = 15;

	public ReplayRecorder ReplayRecorder;

	public ReplayPlayer ReplayPlayer;

	public override void Awake()
	{
		base.Awake();
		ReplayRecorder = GetComponent<ReplayRecorder>();
		ReplayPlayer = GetComponent<ReplayPlayer>();
	}

	public void Server_StartRecording()
	{
		ReplayRecorder.Server_StartRecording(tickRate);
	}

	public void Server_StopRecording()
	{
		ReplayRecorder.Server_StopRecording();
	}

	public void Server_StartReplaying(float secondsToReplay)
	{
		SortedList<int, List<(string, object)>> sortedList = new SortedList<int, List<(string, object)>>(ReplayRecorder.EventMap);
		if (sortedList.Count != 0)
		{
			int num = sortedList.Keys.Max();
			int num2 = (int)((float)tickRate * secondsToReplay);
			int fromTick = num - num2;
			ReplayPlayer.Server_StartReplay(sortedList, tickRate, fromTick);
		}
	}

	public void Server_StopReplaying()
	{
		ReplayPlayer.Server_StopReplay();
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
		return "ReplayManager";
	}
}
