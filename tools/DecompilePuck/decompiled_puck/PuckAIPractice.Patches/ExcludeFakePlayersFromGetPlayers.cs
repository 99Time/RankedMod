using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PuckAIPractice.Utilities;

namespace PuckAIPractice.Patches;

[HarmonyPatch(typeof(PlayerManager), "GetPlayers")]
public static class ExcludeFakePlayersFromGetPlayers
{
	[HarmonyPostfix]
	public static void Postfix(ref List<Player> __result, bool includeReplay)
	{
		__result = __result.Where((Player p) => !FakePlayerRegistry.IsFake(p)).ToList();
	}
}
