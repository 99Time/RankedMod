using HarmonyLib;
using UnityEngine;

namespace PuckAIPractice;

[HarmonyPatch(typeof(GameManager), "OnGameStateChanged")]
public static class PhaseChangePatch
{
	[HarmonyPostfix]
	public static void Postfix(GameManager __instance, GameState oldGameState, GameState newGameState)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Invalid comparison between Unknown and I4
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Invalid comparison between Unknown and I4
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if (oldGameState.Phase != newGameState.Phase)
		{
			if ((int)newGameState.Phase != 1 && (int)newGameState.Phase != 3)
			{
				Debug.Log((object)$"[FAKE_SPAWN] Skipped spawn — new state is {newGameState.Phase}");
			}
			else
			{
				Debug.Log((object)$"[FAKE_SPAWN] State changed from {oldGameState.Phase} to {newGameState.Phase}. Injecting fake players...");
			}
		}
	}
}
