using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using HarmonyLib;
using PuckAIPractice.AI;
using UnityEngine;

namespace PuckAIPractice.Patches;

public static class SimulateDashHelper
{
	public class SimulateDashState : MonoBehaviour
	{
		public Tween MoveTween;

		public Tween DragTween;

		public Tween LegTween;

		public bool IsDashing;
	}

	private static readonly MethodInfo updateAudioMethod = typeof(PlayerBodyV2).GetMethod("Server_UpdateAudio", BindingFlags.Instance | BindingFlags.NonPublic);

	private static Vector3 redNetCenter = new Vector3(0f, 0.8f, -40.23f);

	private static Vector3 blueNetCenter = new Vector3(0f, 0.8f, 40.23f);

	private static float overshootDistanceThreshold = 2f;

	public static bool IsBehindNetRed;

	public static float SignedLateralOffsetRed;

	public static bool IsBehindNetBlue;

	public static float SignedLateralOffsetBlue;

	public static Vector3 ProjectedPointRed;

	public static Vector3 ProjectedPointBlue;

	public static bool SimulateDash(PlayerBodyV2 __instance, Vector3 dashDir)
	{
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Expected O, but got Unknown
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Expected O, but got Unknown
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		SimulateDashState simulateDashState = ((Component)__instance).GetComponent<SimulateDashState>() ?? ((Component)__instance).gameObject.AddComponent<SimulateDashState>();
		Traverse val = Traverse.Create((object)__instance);
		if (!val.Field("canDash").GetValue<bool>() || !__instance.IsSliding.Value)
		{
			return false;
		}
		simulateDashState.IsDashing = true;
		float stamina = __instance.Stamina;
		float value = val.Field("dashStaminaDrain").GetValue<float>();
		Rigidbody rigidbody = __instance.Rigidbody;
		float value2 = val.Field("dashVelocity").GetValue<float>();
		float value3 = val.Field("dashDragTime").GetValue<float>();
		float z = rigidbody.position.z;
		Vector3 target = rigidbody.position + dashDir * value2;
		target.z = z;
		Tween moveTween = simulateDashState.MoveTween;
		if (moveTween != null)
		{
			TweenExtensions.Kill(moveTween, false);
		}
		Tween dragTween = simulateDashState.DragTween;
		if (dragTween != null)
		{
			TweenExtensions.Kill(dragTween, false);
		}
		Tween legTween = simulateDashState.LegTween;
		if (legTween != null)
		{
			TweenExtensions.Kill(legTween, false);
		}
		((MonoBehaviour)__instance).StartCoroutine(MoveFakePlayer(__instance, target, value3));
		val.Field("dashMoveTween").SetValue((object)simulateDashState.MoveTween);
		simulateDashState.DragTween = (Tween)(object)TweenSettingsExtensions.SetEase<TweenerCore<float, float, FloatOptions>>(TweenSettingsExtensions.OnComplete<TweenerCore<float, float, FloatOptions>>(DOTween.To((DOGetter<float>)(() => __instance.Movement.AmbientDrag), (DOSetter<float>)delegate(float v)
		{
			__instance.Movement.AmbientDrag = v;
		}, 0f, value3), (TweenCallback)delegate
		{
			__instance.HasDashed = false;
		}), (Ease)1);
		val.Field("dashDragTween").SetValue((object)simulateDashState.DragTween);
		simulateDashState.LegTween = DOVirtual.DelayedCall(value3 / 4f, (TweenCallback)delegate
		{
			__instance.HasDashExtended = false;
		}, true);
		val.Field("dashLegPadTween").SetValue((object)simulateDashState.LegTween);
		__instance.HasDashed = true;
		__instance.Movement.AmbientDrag = val.Field("dashDrag").GetValue<float>();
		__instance.HasDashExtended = true;
		__instance.IsExtendedLeft.Value = dashDir.x < 0f;
		__instance.IsExtendedRight.Value = dashDir.x > 0f;
		return true;
	}

	private static IEnumerator MoveFakePlayer(PlayerBodyV2 body, Vector3 target, float duration)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		float elapsed = 0f;
		Vector3 start = ((Component)body).transform.position;
		List<Vector3> positionHistory = new List<Vector3>();
		SimulateDashState state = ((Component)body).GetComponent<SimulateDashState>() ?? ((Component)body).gameObject.AddComponent<SimulateDashState>();
		state.IsDashing = true;
		body.IsSliding.Value = true;
		body.HasSlipped = true;
		body.HasFallen = false;
		body.Speed = body.Movement.Speed;
		body.Movement.Sprint = body.IsSprinting.Value;
		body.Movement.TurnMultiplier = (body.IsSliding.Value ? 2f : (body.IsJumping ? 5f : 1f));
		body.Movement.AmbientDrag = (body.HasFallen ? 0.2f : (body.HasDashed ? body.Movement.AmbientDrag : (body.IsStopping.Value ? 2.5f : (body.IsSliding.Value ? 0.2f : 0f))));
		body.Hover.TargetDistance = (body.IsSliding.Value ? 0.8f : (body.KeepUpright.Balance * 1.2f));
		body.Skate.Intensity = ((body.IsSliding.Value || body.IsStopping.Value || !body.IsGrounded) ? 0f : body.KeepUpright.Balance);
		body.VelocityLean.AngularIntensity = Mathf.Max(0.1f, body.Movement.NormalizedMaximumSpeed) / (body.IsSliding.Value ? 2f : (body.IsJumping ? 2f : 1f));
		body.VelocityLean.Inverted = !body.IsJumping && !body.IsSliding.Value && body.Movement.IsMovingBackwards;
		body.VelocityLean.UseWorldLinearVelocity = body.IsJumping || body.IsSliding.Value;
		updateAudioMethod?.Invoke(body, null);
		((Component)body).GetComponent<GoalieAI>();
		while (elapsed < duration && state.IsDashing)
		{
			if ((int)body.Player.Team.Value != 3)
			{
				_ = blueNetCenter;
			}
			else
			{
				_ = redNetCenter;
			}
			Vector3 pos = ((Component)body).transform.position;
			positionHistory.Add(pos);
			if (positionHistory.Count > 5)
			{
				positionHistory.RemoveAt(0);
			}
			Vector3 historyMoveDir = Vector3.zero;
			Vector3 val;
			if (positionHistory.Count >= 2)
			{
				val = pos - positionHistory[0];
				historyMoveDir = ((Vector3)(ref val)).normalized;
			}
			Vector3.Distance(start, ((int)body.Player.Team.Value == 3) ? ProjectedPointRed : ProjectedPointBlue);
			pos.y = 0f;
			if ((int)body.Player.Team.Value == 3)
			{
				ProjectedPointRed.y = 0f;
			}
			else
			{
				ProjectedPointBlue.y = 0f;
			}
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			val = (((int)body.Player.Team.Value == 3) ? ProjectedPointRed : ProjectedPointBlue) - pos;
			Vector3 toTarget = ((Vector3)(ref val)).normalized;
			float alignment = Vector3.Dot(historyMoveDir, toTarget);
			if (((int)body.Player.Team.Value == 3) ? IsBehindNetRed : IsBehindNetBlue)
			{
				Vector3 goalRight = (((int)body.Player.Team.Value == 3) ? Vector3.left : Vector3.right);
				float signedOffset = (((int)body.Player.Team.Value == 3) ? SignedLateralOffsetRed : SignedLateralOffsetBlue);
				if (((Vector3)(ref historyMoveDir)).sqrMagnitude > 0.001f)
				{
					float directionalAlignment = Vector3.Dot(historyMoveDir, goalRight * Mathf.Sign(signedOffset));
					if (directionalAlignment < -0.5f)
					{
						break;
					}
				}
			}
			if (t > 0.05f && alignment < -0.5f)
			{
				break;
			}
			((Component)body).transform.position = Vector3.Lerp(start, target, EaseOutQuad(t));
			updateAudioMethod?.Invoke(body, null);
			yield return null;
		}
		state.IsDashing = false;
		body.IsSliding.Value = false;
		body.HasSlipped = false;
		updateAudioMethod?.Invoke(body, null);
	}

	private static float EaseOutQuad(float t)
	{
		return 1f - (1f - t) * (1f - t);
	}
}
