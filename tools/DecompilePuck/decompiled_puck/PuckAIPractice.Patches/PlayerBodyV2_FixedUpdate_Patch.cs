using System;
using System.Reflection;
using HarmonyLib;
using PuckAIPractice.Utilities;
using UnityEngine;

namespace PuckAIPractice.Patches;

[HarmonyPatch(typeof(PlayerBodyV2), "FixedUpdate")]
public static class PlayerBodyV2_FixedUpdate_Patch
{
	[HarmonyPrefix]
	public static bool Prefix(PlayerBodyV2 __instance)
	{
		if (FakePlayerRegistry.IsFake(__instance.Player))
		{
			RunCustomFixedUpdate(__instance);
			return false;
		}
		return true;
	}

	private static void RunCustomFixedUpdate(PlayerBodyV2 __instance)
	{
		if (!Object.op_Implicit((Object)(object)__instance.Player))
		{
			return;
		}
		PlayerInput playerInput = __instance.Player.PlayerInput;
		if (!Object.op_Implicit((Object)(object)playerInput))
		{
			return;
		}
		Traverse val = Traverse.Create((object)__instance);
		if (!__instance.Player.IsReplay.Value)
		{
			try
			{
				Traverse val2 = val.Method("HandleInputs", new Type[1] { typeof(PlayerInput) }, (object[])null);
				((object)__instance).GetType().GetMethod("HandleInputs", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(__instance, new object[1] { playerInput });
			}
			catch
			{
			}
		}
		typeof(PlayerBodyV2).GetMethod("Server_UpdateAudio", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(__instance, null);
	}
}
