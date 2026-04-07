using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PuckAIPractice.Patches;
using PuckAIPractice.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace PuckAIPractice.AI;

public class GoalieAI : MonoBehaviour
{
	private Vector3 redGoal = new Vector3(0f, 0f, -40.23f);

	private Vector3 blueGoal = new Vector3(0f, 0f, 40.23f);

	public Player controlledPlayer;

	public Transform puckTransform;

	public PlayerTeam team;

	private GameObject puckLine;

	private bool lineInitialized = false;

	private float lastDashStartTime = float.NegativeInfinity;

	private float lastDashTime = float.NegativeInfinity;

	private bool dashLeftNext = true;

	private PlayerBodyV2 body;

	private Vector3? targetCancelPosition = null;

	private float cancelInterpSpeed = 20f;

	private bool isPreparingDash = false;

	private Vector3 pendingDashDir;

	private float dashReadyThreshold = 5f;

	private bool hasDashed = false;

	private bool isDashing = false;

	private GameObject interceptTargetSphere;

	private Vector3 lastComputedIntercept;

	private GameObject netForwardArrow;

	private GameObject netToPuckArrow;

	private GameObject leftBoundArrow;

	private GameObject rightBoundArrow;

	private void Start()
	{
		Debug.Log((object)"Goalie AI Started");
		if ((Object)(object)controlledPlayer != (Object)null)
		{
			body = controlledPlayer.PlayerBody;
		}
	}

	private IEnumerator DelayedSlide()
	{
		yield return (object)new WaitForSeconds(3f);
		PlayerBodyV2 body = controlledPlayer.PlayerBody;
		if ((Object)(object)body != (Object)null)
		{
			body.IsSliding.Value = true;
		}
	}

	public Vector3 GetInterceptPoint()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		return lastComputedIntercept;
	}

	public Puck GetClosestPuck(Vector3 position, bool includeReplay = false)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		List<Puck> pucks = NetworkBehaviourSingleton<PuckManager>.Instance.GetPucks(false);
		List<Puck> list = (includeReplay ? pucks : pucks.Where((Puck puck) => !puck.IsReplay.Value).ToList());
		Puck result = null;
		float num = float.MaxValue;
		foreach (Puck item in list)
		{
			float num2 = Vector3.Distance(position, ((Component)item).transform.position);
			if (num2 < num)
			{
				result = item;
				num = num2;
			}
		}
		return result;
	}

	private void Update()
	{
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Invalid comparison between Unknown and I4
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Invalid comparison between Unknown and I4
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Invalid comparison between Unknown and I4
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Invalid comparison between Unknown and I4
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Invalid comparison between Unknown and I4
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Invalid comparison between Unknown and I4
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Invalid comparison between Unknown and I4
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ef: Invalid comparison between Unknown and I4
		//IL_0303: Unknown result type (might be due to invalid IL or missing references)
		//IL_0305: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0316: Unknown result type (might be due to invalid IL or missing references)
		//IL_031c: Invalid comparison between Unknown and I4
		//IL_0339: Unknown result type (might be due to invalid IL or missing references)
		//IL_033b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0341: Unknown result type (might be due to invalid IL or missing references)
		//IL_0343: Unknown result type (might be due to invalid IL or missing references)
		//IL_0348: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)controlledPlayer == (Object)null || !FakePlayerRegistry.All.Contains(controlledPlayer))
		{
			return;
		}
		if (PracticeModeDetector.IsPracticeMode)
		{
			ResolvePuckReference();
		}
		else
		{
			Puck closestPuck = GetClosestPuck(((int)controlledPlayer.Team.Value == 2) ? blueGoal : redGoal);
			if ((Object)(object)closestPuck != (Object)null)
			{
				puckTransform = ((Component)closestPuck).transform;
			}
			else
			{
				Puck puck = NetworkBehaviourSingleton<PuckManager>.Instance.GetPuck(false);
				if ((Object)(object)puck == (Object)null)
				{
					return;
				}
				puckTransform = ((Component)puck).transform;
			}
		}
		InitializeBody();
		Quaternion neutralRotation = GetNeutralRotation();
		Vector3 neutralForward = GetNeutralForward();
		Vector3 position = ((Component)controlledPlayer.PlayerBody.Rigidbody).transform.position;
		Transform obj = puckTransform;
		Vector3 val = ((obj != null) ? obj.position : Vector3.zero);
		Vector3 toPuck = val - position;
		toPuck.y = 0f;
		Vector3 val2 = (((int)controlledPlayer.Team.Value == 3) ? redGoal : blueGoal);
		Vector3 val3 = val - position;
		Vector3 val4 = neutralForward;
		Vector3 val5 = val - val2;
		Vector3 val6 = (((int)controlledPlayer.Team.Value == 3) ? Vector3.back : Vector3.forward);
		float num = Vector3.Dot(neutralForward, ((Vector3)(ref val5)).normalized);
		if (num < 0.5f)
		{
			if ((int)controlledPlayer.Team.Value == 3)
			{
				SimulateDashHelper.IsBehindNetRed = true;
			}
			else
			{
				SimulateDashHelper.IsBehindNetBlue = true;
			}
		}
		else if ((int)controlledPlayer.Team.Value == 3)
		{
			SimulateDashHelper.IsBehindNetRed = false;
		}
		else
		{
			SimulateDashHelper.IsBehindNetBlue = false;
		}
		GoalieSettings goalieSettings = (((int)controlledPlayer.Team.Value == 3) ? GoalieSettings.InstanceRed : GoalieSettings.InstanceBlue);
		float maxAngle = goalieSettings.MaxRotationAngle;
		Vector3 goalRight = (((int)controlledPlayer.Team.Value == 3) ? Vector3.left : Vector3.right);
		bool flag = num < 0.5f;
		if (!flag)
		{
			float num2 = Vector3.Distance(val, val2);
			float num3 = 6f;
			float num4 = 2.5f;
			float num5 = Mathf.InverseLerp(num4, num3, num2);
			maxAngle = Mathf.Lerp(0f, goalieSettings.MaxRotationAngle, num5);
		}
		if (!isPreparingDash)
		{
			RotateTowardPuck(toPuck, neutralForward, flag, maxAngle, val2, goalRight);
		}
		Color val7 = ((num >= 0.5f) ? Color.green : Color.red);
		float signedLateralOffset;
		float lateralDistance;
		Vector3 puckToGoalDir;
		Vector3 projectedInterceptClamped = GetProjectedInterceptClamped(position, val, val2, num, out signedLateralOffset, out lateralDistance, out puckToGoalDir);
		if ((int)controlledPlayer.Team.Value == 3)
		{
			SimulateDashHelper.ProjectedPointRed = projectedInterceptClamped;
		}
		else
		{
			SimulateDashHelper.ProjectedPointBlue = projectedInterceptClamped;
		}
		if ((int)controlledPlayer.Team.Value == 3)
		{
			SimulateDashHelper.SignedLateralOffsetRed = signedLateralOffset;
		}
		else
		{
			SimulateDashHelper.SignedLateralOffsetBlue = signedLateralOffset;
		}
		lastComputedIntercept = projectedInterceptClamped;
		HandleDashLogic(toPuck, neutralRotation, lateralDistance, signedLateralOffset, projectedInterceptClamped);
	}

	private void ResolvePuckReference()
	{
		Puck playerPuck = NetworkBehaviourSingleton<PuckManager>.Instance.GetPlayerPuck(((NetworkBehaviour)NetworkBehaviourSingleton<PuckManager>.Instance).OwnerClientId);
		if ((Object)(object)playerPuck != (Object)null)
		{
			puckTransform = ((Component)playerPuck).transform;
		}
	}

	private void InitializeBody()
	{
		if (!((Object)(object)body != (Object)null))
		{
			body = ((Component)controlledPlayer).GetComponent<PlayerBodyV2>();
		}
	}

	private void InitializeInterceptVisuals()
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)interceptTargetSphere != (Object)null))
		{
			interceptTargetSphere = GameObject.CreatePrimitive((PrimitiveType)0);
			((Object)interceptTargetSphere).name = "InterceptTargetSphere";
			interceptTargetSphere.GetComponent<Collider>().enabled = false;
			Material val = new Material(Shader.Find("Sprites/Default"));
			val.color = new Color(1f, 0.5f, 0f, 0.7f);
			interceptTargetSphere.GetComponent<Renderer>().material = val;
			interceptTargetSphere.transform.localScale = Vector3.one * 0.3f;
		}
	}

	private Quaternion GetNeutralRotation()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		return Quaternion.LookRotation(((int)controlledPlayer.Team.Value == 3) ? Vector3.forward : Vector3.back, Vector3.up);
	}

	private Vector3 GetNeutralForward()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		return ((int)controlledPlayer.Team.Value == 3) ? Vector3.forward : Vector3.back;
	}

	private void RotateTowardPuck(Vector3 toPuck, Vector3 neutralForward, bool isBehindNet, float maxAngle, Vector3 goalCenter, Vector3 goalRight)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		GoalieSettings goalieSettings = (((int)controlledPlayer.Team.Value == 3) ? GoalieSettings.InstanceRed : GoalieSettings.InstanceBlue);
		if (isBehindNet)
		{
			Vector3 val = puckTransform.position - goalCenter;
			float num = Vector3.Dot(val, goalRight);
			Vector3 val2 = ((num < 0f) ? (-goalRight) : goalRight);
			float num2 = Vector3.SignedAngle(neutralForward, val2, Vector3.up);
			float num3 = Mathf.Clamp(num2, 0f - maxAngle, maxAngle);
			Quaternion val3 = Quaternion.AngleAxis(num3, Vector3.up) * Quaternion.LookRotation(neutralForward);
			((Component)body).transform.rotation = Quaternion.Slerp(((Component)body).transform.rotation, val3, Time.deltaTime * goalieSettings.RotationSpeed);
		}
		else if (!(((Vector3)(ref toPuck)).sqrMagnitude <= 0.01f))
		{
			Vector3 normalized = ((Vector3)(ref toPuck)).normalized;
			float num4 = Vector3.SignedAngle(neutralForward, normalized, Vector3.up);
			float num5 = Mathf.Clamp(num4, 0f - maxAngle, maxAngle);
			Quaternion val4 = Quaternion.AngleAxis(num5, Vector3.up) * Quaternion.LookRotation(neutralForward);
			((Component)body).transform.rotation = Quaternion.Slerp(((Component)body).transform.rotation, val4, Time.deltaTime * goalieSettings.RotationSpeed);
		}
	}

	private void UpdateArrow(GameObject obj, Vector3 start, Vector3 end, Color dotColor)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = end - start;
		Vector3 position = start + val / 2f;
		obj.transform.position = position;
		obj.transform.rotation = Quaternion.FromToRotation(Vector3.up, val);
		obj.transform.localScale = new Vector3(0.1f, ((Vector3)(ref val)).magnitude / 2f, 0.1f);
		Renderer component = obj.GetComponent<Renderer>();
		if ((Object)(object)component != (Object)null)
		{
			component.material.color = dotColor;
		}
	}

	private void CreateArrow(ref GameObject obj, Color color)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)obj == (Object)null)
		{
			Material material = new Material(Shader.Find("Sprites/Default"));
			obj = GameObject.CreatePrimitive((PrimitiveType)2);
			obj.transform.localScale = new Vector3(0.1f, 2f, 0.1f);
			obj.GetComponent<Renderer>().material = material;
			obj.GetComponent<Renderer>().material.color = color;
		}
	}

	private Vector3 GetProjectedInterceptClamped(Vector3 goaliePos, Vector3 puckPos, Vector3 goalCenter, float forwardDot, out float signedLateralOffset, out float lateralDistance, out Vector3 puckToGoalDir)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Invalid comparison between Unknown and I4
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Invalid comparison between Unknown and I4
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		GoalieSettings goalieSettings = (((int)controlledPlayer.Team.Value == 3) ? GoalieSettings.InstanceRed : GoalieSettings.InstanceBlue);
		Vector3 val = goalCenter - puckPos;
		puckToGoalDir = ((Vector3)(ref val)).normalized;
		Vector3 val2 = Vector3.Cross(Vector3.up, puckToGoalDir);
		Vector3 val3 = (((int)controlledPlayer.Team.Value == 3) ? Vector3.left : Vector3.right);
		if (forwardDot < 0.5f)
		{
			Vector3 val4 = puckPos - goalCenter;
			float num = Vector3.Dot(val4, val3);
			float num2 = 1.5f;
			float num3 = 0f - goalieSettings.DistanceFromNet;
			Vector3 val5 = goalCenter + val3 * Mathf.Sign(num) * num2;
			val5.z += (((int)controlledPlayer.Team.Value == 3) ? (0f - num3) : num3);
			val5.y = goaliePos.y;
			signedLateralOffset = Vector3.Dot(val5 - goaliePos, val3);
			lateralDistance = Mathf.Abs(signedLateralOffset);
			if ((Object)(object)interceptTargetSphere != (Object)null)
			{
				interceptTargetSphere.transform.position = val5;
			}
			return val5;
		}
		val = goalCenter - puckPos;
		if (Mathf.Abs(val.z) < 0.001f)
		{
			val.z = 0.001f;
		}
		float num4 = (goaliePos.z - puckPos.z) / val.z;
		Vector3 val6 = puckPos + val * num4;
		val6.y = goaliePos.y;
		Vector3 val7 = val6 - goaliePos;
		signedLateralOffset = Vector3.Dot(val7, val2);
		signedLateralOffset = Mathf.Clamp(signedLateralOffset, -3.5f, 3.5f);
		lateralDistance = Mathf.Abs(signedLateralOffset);
		if ((Object)(object)interceptTargetSphere != (Object)null)
		{
			interceptTargetSphere.transform.position = val6;
		}
		return val6;
	}

	private Vector3 GetProjectedInterceptClamped(Vector3 goaliePos, Vector3 puckPos, Vector3 goalCenter, out float signedLateralOffset, out float lateralDistance, out Vector3 puckToGoalDir)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = goalCenter - puckPos;
		puckToGoalDir = ((Vector3)(ref val)).normalized;
		Vector3 val2 = goaliePos - puckPos;
		float num = Vector3.Dot(val2, puckToGoalDir);
		num = Mathf.Clamp(num, 0f, 60f);
		Vector3 val3 = puckPos + puckToGoalDir * num;
		val3.y = goaliePos.y;
		Vector3 val4 = Vector3.Cross(Vector3.up, puckToGoalDir);
		Vector3 val5 = val3 - goaliePos;
		signedLateralOffset = Vector3.Dot(val5, val4);
		lateralDistance = ((Vector3)(ref val5)).magnitude;
		float num2 = 3.5f;
		signedLateralOffset = Mathf.Clamp(signedLateralOffset, 0f - num2, num2);
		Vector3 val6 = val4 * signedLateralOffset;
		if ((Object)(object)interceptTargetSphere != (Object)null)
		{
			interceptTargetSphere.transform.position = goaliePos + val6;
		}
		return goaliePos + val6;
	}

	private void HandleDashLogic(Vector3 toPuck, Quaternion neutralRotation, float lateralDistance, float signedLateralOffset, Vector3 projectedPoint)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Invalid comparison between Unknown and I4
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Invalid comparison between Unknown and I4
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Invalid comparison between Unknown and I4
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		GoalieSettings goalieSettings = (((int)controlledPlayer.Team.Value == 3) ? GoalieSettings.InstanceRed : GoalieSettings.InstanceBlue);
		bool flag = Time.time < lastDashTime + goalieSettings.DashCooldown;
		bool flag2 = Time.time < lastDashStartTime + goalieSettings.DashCancelGrace;
		controlledPlayer.PlayerInput.Client_SlideInputRpc(true);
		if (!flag && lateralDistance > goalieSettings.DashThreshold && !isPreparingDash)
		{
			Vector3 val = ((signedLateralOffset < 0f) ? Vector3.left : Vector3.right);
			Vector3 val2 = (((int)controlledPlayer.Team.Value == 3) ? Vector3.left : Vector3.right);
			Vector3 val3 = val2 * Mathf.Sign(signedLateralOffset);
			pendingDashDir = val3;
			isPreparingDash = true;
		}
		else if (isPreparingDash)
		{
			((Component)body).transform.rotation = Quaternion.RotateTowards(((Component)body).transform.rotation, neutralRotation, Time.deltaTime * goalieSettings.RotationSpeed * 60f);
			float num = Quaternion.Angle(((Component)body).transform.rotation, neutralRotation);
			if (num <= dashReadyThreshold)
			{
				if ((pendingDashDir.x < 0f && (int)controlledPlayer.Team.Value == 3) || (pendingDashDir.x >= 0f && (int)controlledPlayer.Team.Value == 2))
				{
					controlledPlayer.PlayerInput.Client_DashLeftInputRpc();
					hasDashed = true;
				}
				else
				{
					controlledPlayer.PlayerInput.Client_DashRightInputRpc();
					hasDashed = true;
				}
				lastDashTime = Time.time;
				isPreparingDash = false;
			}
		}
		else if (Mathf.Abs(signedLateralOffset) <= goalieSettings.CancelThreshold && hasDashed)
		{
			hasDashed = false;
			body.CancelDash();
			targetCancelPosition = projectedPoint;
		}
	}

	private void UpdatePuckLine(Vector3 start, Vector3 end)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		if (!lineInitialized)
		{
			puckLine = GameObject.CreatePrimitive((PrimitiveType)3);
			((Object)puckLine).name = "PuckInterceptLine";
			puckLine.GetComponent<Collider>().enabled = false;
			Renderer component = puckLine.GetComponent<Renderer>();
			component.material = new Material(Shader.Find("Sprites/Default"));
			component.material.color = new Color(0f, 1f, 1f, 0.5f);
			lineInitialized = true;
		}
		if ((Object)(object)puckLine != (Object)null)
		{
			Vector3 position = (start + end) / 2f;
			Vector3 val = end - start;
			float magnitude = ((Vector3)(ref val)).magnitude;
			puckLine.transform.position = position;
			puckLine.transform.rotation = Quaternion.LookRotation(((Vector3)(ref val)).normalized);
			puckLine.transform.localScale = new Vector3(0.05f, 0.05f, magnitude);
		}
	}

	private void UpdateInterceptVisual(Vector3 projectedPoint)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)interceptTargetSphere != (Object)null)
		{
			interceptTargetSphere.transform.position = projectedPoint;
		}
	}
}
