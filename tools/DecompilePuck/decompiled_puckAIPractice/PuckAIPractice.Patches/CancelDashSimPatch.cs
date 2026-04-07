using DG.Tweening;
using HarmonyLib;
using PuckAIPractice.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace PuckAIPractice.Patches;

[HarmonyPatch(typeof(PlayerBodyV2), "CancelDash")]
public static class CancelDashSimPatch
{
	[HarmonyPrefix]
	public static bool Prefix(PlayerBodyV2 __instance)
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return false;
		}
		if (!FakePlayerRegistry.IsFake(__instance.Player))
		{
			return true;
		}
		SimulateDashHelper.SimulateDashState component = ((Component)__instance).GetComponent<SimulateDashHelper.SimulateDashState>();
		if ((Object)(object)component == (Object)null)
		{
			return false;
		}
		Tween moveTween = component.MoveTween;
		if (moveTween != null)
		{
			TweenExtensions.Kill(moveTween, false);
		}
		component.MoveTween = null;
		Tween dragTween = component.DragTween;
		if (dragTween != null)
		{
			TweenExtensions.Kill(dragTween, false);
		}
		component.DragTween = null;
		Tween legTween = component.LegTween;
		if (legTween != null)
		{
			TweenExtensions.Kill(legTween, false);
		}
		component.LegTween = null;
		component.IsDashing = false;
		__instance.HasDashed = false;
		__instance.HasDashExtended = false;
		__instance.IsExtendedLeft.Value = false;
		__instance.IsExtendedRight.Value = false;
		__instance.Movement.AmbientDrag = 0f;
		return false;
	}
}
