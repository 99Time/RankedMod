using System.Collections;
using PuckAIPractice.Utilities;
using UnityEngine;

namespace PuckAIPractice.GameModes;

public static class Goalies
{
	public static bool GoaliesAreRunning;

	public static IEnumerator StartGoalieSession()
	{
		BotSpawning.SpawnFakePlayer(0, (PlayerRole)2, (PlayerTeam)2);
		yield return (object)new WaitForSeconds(0.1f);
		BotSpawning.SpawnFakePlayer(1, (PlayerRole)2, (PlayerTeam)3);
		GoaliesAreRunning = true;
	}

	public static IEnumerator StartGoalieSessionRed()
	{
		yield return (object)new WaitForSeconds(0.1f);
		BotSpawning.SpawnFakePlayer(1, (PlayerRole)2, (PlayerTeam)3);
		GoaliesAreRunning = true;
	}

	public static IEnumerator StartGoalieSessionBlue()
	{
		yield return (object)new WaitForSeconds(0.1f);
		BotSpawning.SpawnFakePlayer(0, (PlayerRole)2, (PlayerTeam)2);
		GoaliesAreRunning = true;
	}

	public static void StartGoalieSessionViaCoroutine(GoalieSession session)
	{
		switch (session)
		{
		case GoalieSession.Blue:
			((MonoBehaviour)GoalieRunner.Instance).StartCoroutine(StartGoalieSessionBlue());
			break;
		case GoalieSession.Red:
			((MonoBehaviour)GoalieRunner.Instance).StartCoroutine(StartGoalieSessionRed());
			break;
		case GoalieSession.Both:
			((MonoBehaviour)GoalieRunner.Instance).StartCoroutine(StartGoalieSession());
			break;
		}
	}

	public static void EndGoalieSession(GoalieSession type)
	{
		BotSpawning.DespawnBots(type);
		GoaliesAreRunning = false;
	}
}
