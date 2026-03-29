using HarmonyLib;
using PuckAIPractice.Utilities;
using UnityEngine;

namespace PuckAIPractice.Patches;

[HarmonyPatch(typeof(Movement), "FixedUpdate")]
public class MovementFixedUpdatePatch
{
	private static bool Prefix(Movement __instance)
	{
		PlayerBodyV2 component = ((Component)__instance).GetComponent<PlayerBodyV2>();
		if ((Object)(object)component != (Object)null && FakePlayerRegistry.IsFake(__instance.PlayerBody.Player))
		{
			return false;
		}
		return true;
	}
}
