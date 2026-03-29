using AYellowpaper.SerializedCollections;
using DG.Tweening;
using DG.Tweening.CustomPlugins;
using UnityEngine;

public class PlayerLegPad : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private SerializedDictionary<PlayerLegPadState, Transform> positions = new SerializedDictionary<PlayerLegPadState, Transform>();

	[Header("Settings")]
	[SerializeField]
	private float raycastDistance = 1f;

	[SerializeField]
	private float transitionDuration = 0.15f;

	[Space(20f)]
	[SerializeField]
	private LayerMask raycastLayerMask;

	private PlayerLegPadState state;

	private Vector3 localPosition = Vector3.zero;

	private float localYPosition;

	private Quaternion localRotation = Quaternion.identity;

	private Tween localPositionTween;

	private Tween localRotationTween;

	[HideInInspector]
	public PlayerLegPadState State
	{
		get
		{
			return state;
		}
		set
		{
			OnStateChanged(state, value);
			state = value;
		}
	}

	private void Awake()
	{
		localPosition = base.transform.localPosition;
		localRotation = base.transform.localRotation;
	}

	private void Update()
	{
		ShootLegPadRaycast();
		base.transform.localPosition = new Vector3(localPosition.x, localYPosition, localPosition.z);
		base.transform.localRotation = localRotation;
	}

	private void FixedUpdate()
	{
	}

	private void OnDestroy()
	{
		localPositionTween?.Kill();
		localRotationTween?.Kill();
	}

	public void ShootLegPadRaycast()
	{
		Vector3 vector = base.transform.parent.TransformPoint(localPosition);
		vector.y = base.transform.parent.position.y;
		vector += base.transform.parent.up;
		Vector3 vector2 = -base.transform.parent.up;
		Debug.DrawRay(vector, vector2 * raycastDistance, Color.red);
		if (Physics.Raycast(vector, vector2, out var hitInfo, raycastDistance, raycastLayerMask))
		{
			localYPosition = base.transform.parent.InverseTransformPoint(hitInfo.point).y + localPosition.y;
		}
		else
		{
			localYPosition = base.transform.parent.InverseTransformPoint(vector + vector2 * raycastDistance).y + localPosition.y;
		}
	}

	private void OnStateChanged(PlayerLegPadState oldState, PlayerLegPadState newState)
	{
		localPositionTween?.Kill();
		localRotationTween?.Kill();
		if (oldState == PlayerLegPadState.Butterfly && newState == PlayerLegPadState.ButterflyExtended)
		{
			localPosition = positions[oldState].localPosition;
			localRotation = positions[oldState].localRotation;
		}
		localPositionTween = DOTween.To(() => localPosition, delegate(Vector3 value)
		{
			localPosition = value;
		}, positions[newState].localPosition, transitionDuration);
		localRotationTween = DOTween.To(PureQuaternionPlugin.Plug(), () => localRotation, delegate(Quaternion value)
		{
			localRotation = value;
		}, positions[newState].localRotation, transitionDuration);
	}
}
