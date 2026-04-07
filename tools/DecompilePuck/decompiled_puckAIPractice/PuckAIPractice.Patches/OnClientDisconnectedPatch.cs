using System;
using System.Collections.Generic;
using HarmonyLib;

namespace PuckAIPractice.Patches;

[HarmonyPatch(typeof(ServerManagerController), "Event_OnClientDisconnected")]
[HarmonyPatch(new Type[] { typeof(Dictionary<string, object>) })]
public static class OnClientDisconnectedPatch
{
	public static void Postfix(Dictionary<string, object> message)
	{
	}
}
