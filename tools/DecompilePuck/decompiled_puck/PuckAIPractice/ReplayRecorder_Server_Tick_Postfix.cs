using System;
using HarmonyLib;
using PuckAIPractice.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace PuckAIPractice;

[HarmonyPatch(typeof(ReplayRecorder), "Server_Tick")]
public static class ReplayRecorder_Server_Tick_Postfix
{
	private static void Postfix(ReplayRecorder __instance)
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)NetworkManager.Singleton == (Object)null || !NetworkManager.Singleton.IsServer)
		{
			return;
		}
		try
		{
			foreach (Player item in FakePlayerRegistry.All)
			{
				__instance.Server_AddReplayEvent("PlayerBodyMove", (object)new ReplayPlayerBodyMove
				{
					OwnerClientId = ((NetworkBehaviour)item).OwnerClientId,
					Position = ((Component)item.PlayerBody).transform.position,
					Rotation = ((Component)item.PlayerBody).transform.rotation,
					Stamina = item.PlayerBody.StaminaCompressed.Value,
					Speed = item.PlayerBody.StaminaCompressed.Value,
					IsSprinting = item.PlayerBody.IsSprinting.Value,
					IsSliding = item.PlayerBody.IsSliding.Value,
					IsStopping = item.PlayerBody.IsStopping.Value,
					IsExtendedLeft = item.PlayerBody.IsExtendedLeft.Value,
					IsExtendedRight = item.PlayerBody.IsExtendedRight.Value
				});
				__instance.Server_AddReplayEvent("StickMove", (object)new ReplayStickMove
				{
					OwnerClientId = ((NetworkBehaviour)item).OwnerClientId,
					Position = ((Component)item.Stick).transform.position,
					Rotation = ((Component)item.Stick).transform.rotation
				});
				__instance.Server_AddReplayEvent("PlayerInput", (object)new ReplayPlayerInput
				{
					OwnerClientId = ((NetworkBehaviour)item).OwnerClientId,
					LookAngleInput = item.PlayerInput.LookAngleInput.ServerValue,
					BladeAngleInput = item.PlayerInput.BladeAngleInput.ServerValue,
					TrackInput = item.PlayerInput.TrackInput.ServerValue,
					LookInput = item.PlayerInput.LookInput.ServerValue
				});
			}
		}
		catch (Exception arg)
		{
			Debug.LogError((object)$"[YourMod] Postfix failed: {arg}");
		}
	}
}
