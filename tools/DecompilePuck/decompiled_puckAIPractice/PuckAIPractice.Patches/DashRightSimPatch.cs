using HarmonyLib;
using PuckAIPractice.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace PuckAIPractice.Patches;

[HarmonyPatch(typeof(PlayerBodyV2), "DashRight")]
public static class DashRightSimPatch
{
	[HarmonyPrefix]
	public static bool Prefix(PlayerBodyV2 __instance)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		if (!NetworkManager.Singleton.IsServer || !FakePlayerRegistry.IsFake(__instance.Player))
		{
			return true;
		}
		return !SimulateDashHelper.SimulateDash(__instance, ((Component)__instance).transform.right);
	}
}
