using System.Collections.Generic;
using Codebase;
using UnityEngine;

namespace oomtm450PuckMod_Stats;

internal class PuckRaycast : MonoBehaviour
{
	private const int CHECK_EVERY_X_FRAMES = 6;

	private readonly Vector3 ABOVE_GROUND_VECTOR = new Vector3(0f, 0.001f, 0f);

	private readonly float RIGHT_OFFSET = 0.16f;

	private readonly Vector3 BOTTOM_VECTOR = new Vector3(0f, -0.6f, 0f);

	private readonly LayerMask _goalTriggerlayerMask = GetLayerMask("Goal Trigger");

	private Ray _rayBottomLeft;

	private Ray _rayBottomRight;

	private Ray _rayFarBottomLeft;

	private Ray _rayFarBottomRight;

	private Vector3 _startingPosition;

	private int _increment;

	internal LockDictionary<PlayerTeam, bool> PuckIsGoingToNet { get; set; } = new LockDictionary<PlayerTeam, bool>
	{
		{
			(PlayerTeam)2,
			false
		},
		{
			(PlayerTeam)3,
			false
		}
	};

	internal void Start()
	{
		ResetStartingPosition();
		_increment = 5;
		Update();
	}

	internal void Update()
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		if (++_increment != 6)
		{
			return;
		}
		foreach (PlayerTeam item in new List<PlayerTeam>(PuckIsGoingToNet.Keys))
		{
			PuckIsGoingToNet[item] = false;
		}
		CheckForColliders();
		ResetStartingPosition();
	}

	private void ResetStartingPosition()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		_startingPosition = ((Component)this).transform.position;
		_increment = 0;
	}

	private void CheckForColliders()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		float num = NetworkBehaviourSingleton<PuckManager>.Instance.GetPuck(false).Speed * 3.3f;
		_startingPosition.y = ((Component)this).transform.position.y;
		Vector3 val = ((Component)this).transform.position - _startingPosition;
		Vector3 position = ((Component)this).transform.position;
		Vector3 val2 = Vector3.Cross(Vector3.down, ((Vector3)(ref val)).normalized);
		Vector3 val3 = position + ((Vector3)(ref val2)).normalized * RIGHT_OFFSET;
		_rayBottomLeft = new Ray(val3 + ABOVE_GROUND_VECTOR, val);
		RaycastHit val4 = default(RaycastHit);
		bool flag = Physics.Raycast(_rayBottomLeft, ref val4, num, LayerMask.op_Implicit(_goalTriggerlayerMask), (QueryTriggerInteraction)2);
		if (!flag)
		{
			Vector3 position2 = ((Component)this).transform.position;
			val2 = Vector3.Cross(Vector3.up, ((Vector3)(ref val)).normalized);
			Vector3 val5 = position2 + ((Vector3)(ref val2)).normalized * RIGHT_OFFSET;
			_rayBottomRight = new Ray(val5 + ABOVE_GROUND_VECTOR, val);
			flag = Physics.Raycast(_rayBottomRight, ref val4, num, LayerMask.op_Implicit(_goalTriggerlayerMask), (QueryTriggerInteraction)2);
			if (!flag)
			{
				_rayFarBottomLeft = new Ray(val3 + BOTTOM_VECTOR, val);
				flag = Physics.Raycast(_rayFarBottomLeft, ref val4, num, LayerMask.op_Implicit(_goalTriggerlayerMask), (QueryTriggerInteraction)2);
				if (!flag)
				{
					_rayFarBottomRight = new Ray(val5 + BOTTOM_VECTOR, val);
					flag = Physics.Raycast(_rayFarBottomRight, ref val4, num, LayerMask.op_Implicit(_goalTriggerlayerMask), (QueryTriggerInteraction)2);
					if (!flag)
					{
						return;
					}
				}
			}
		}
		Goal privateField = SystemFunc.GetPrivateField<Goal>(typeof(GoalTrigger), ((Component)((RaycastHit)(ref val4)).collider).gameObject.GetComponent<GoalTrigger>(), "goal");
		PlayerTeam privateField2 = SystemFunc.GetPrivateField<PlayerTeam>(typeof(Goal), privateField, "Team");
		PuckIsGoingToNet[privateField2] = flag;
	}

	private static LayerMask GetLayerMask(string layerName)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		LayerMask val = LayerMask.op_Implicit(0);
		for (int i = 0; i < 32; i++)
		{
			string text = LayerMask.LayerToName(i);
			if (!string.IsNullOrEmpty(text) && text == layerName)
			{
				val = LayerMask.op_Implicit(LayerMask.op_Implicit(val) | (1 << i));
				break;
			}
		}
		return val;
	}
}
