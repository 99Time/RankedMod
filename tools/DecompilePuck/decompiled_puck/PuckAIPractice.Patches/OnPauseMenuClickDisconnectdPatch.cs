using System;
using System.Collections.Generic;
using HarmonyLib;
using PuckAIPractice.GameModes;

namespace PuckAIPractice.Patches;

[HarmonyPatch(typeof(ConnectionManagerController), "Event_Client_OnPauseMenuClickDisconnect")]
[HarmonyPatch(new Type[] { typeof(Dictionary<string, object>) })]
public static class OnPauseMenuClickDisconnectdPatch
{
	public static void Postfix(Dictionary<string, object> message)
	{
		if (Goalies.GoaliesAreRunning)
		{
			Goalies.EndGoalieSession(GoalieSession.Both);
		}
		if (message.TryGetValue("clientId", out var value) && value is ulong && 1 == 0)
		{
		}
	}
}
