using System;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerBodyV2))]
[RequireComponent(typeof(Hover))]
public class Movement : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float forwardsAcceleration = 2f;

	[SerializeField]
	private float forwardsSprintAcceleration = 4.75f;

	[SerializeField]
	private float forwardsSprintOverspeedAcceleration = 1f;

	[SerializeField]
	private float backwardsAcceleration = 1.8f;

	[SerializeField]
	private float backwardsSprintAcceleration = 2f;

	[SerializeField]
	private float backwardsSprintOverspeedAcceleration = 1f;

	[SerializeField]
	private float brakeAcceleration = 5f;

	[SerializeField]
	private float drag = 0.025f;

	[SerializeField]
	private float overspeedDrag = 0.025f;

	[Space(20f)]
	[SerializeField]
	private float maxForwardsSpeed = 7.5f;

	[SerializeField]
	private float maxForwardsSprintSpeed = 8.75f;

	[SerializeField]
	private float maxBackwardsSpeed = 7.25f;

	[SerializeField]
	private float maxBackwardsSprintSpeed = 7.25f;

	[Space(20f)]
	[SerializeField]
	private float turnAcceleration = 1.625f;

	[SerializeField]
	private float turnBrakeAcceleration = 3.25f;

	[SerializeField]
	private float turnMaxSpeed = 1.375f;

	[SerializeField]
	private float turnDrag = 3f;

	[SerializeField]
	private float turnOverspeedDrag = 2.25f;

	[HideInInspector]
	public Rigidbody Rigidbody;

	[HideInInspector]
	public PlayerBodyV2 PlayerBody;

	[HideInInspector]
	public Hover Hover;

	[HideInInspector]
	public bool MoveForwards;

	[HideInInspector]
	public bool MoveBackwards;

	[HideInInspector]
	public bool TurnLeft;

	[HideInInspector]
	public bool TurnRight;

	[HideInInspector]
	public float TurnMultiplier;

	[HideInInspector]
	public bool Sprint;

	[HideInInspector]
	public float AmbientDrag;

	[HideInInspector]
	public Transform MovementDirection;

	private float currentMaxSpeed;

	private float currentAcceleration;

	[HideInInspector]
	public float Speed => new Vector3(Rigidbody.linearVelocity.x, 0f, Rigidbody.linearVelocity.z).magnitude;

	[HideInInspector]
	public float NormalizedMaximumSpeed => Speed / MaximumSpeed;

	[HideInInspector]
	public float NormalizedMinimumSpeed => Speed / MinimumSpeed;

	[HideInInspector]
	public float TurnSpeed => Math.Abs(base.transform.InverseTransformVector(Rigidbody.angularVelocity).y);

	[HideInInspector]
	public float MaximumSpeed => Mathf.Max(maxForwardsSpeed, maxForwardsSprintSpeed, maxBackwardsSpeed, maxBackwardsSprintSpeed);

	[HideInInspector]
	public float MinimumSpeed => Mathf.Min(maxForwardsSpeed, maxForwardsSprintSpeed, maxBackwardsSpeed, maxBackwardsSprintSpeed);

	[HideInInspector]
	public bool IsMovingForwards => MovementDirection.InverseTransformVector(Rigidbody.linearVelocity).z > 0f;

	[HideInInspector]
	public bool IsMovingBackwards => MovementDirection.InverseTransformVector(Rigidbody.linearVelocity).z < 0f;

	[HideInInspector]
	public bool IsTurningLeft => base.transform.InverseTransformVector(Rigidbody.angularVelocity).y < 0f;

	[HideInInspector]
	public bool IsTurningRight => base.transform.InverseTransformVector(Rigidbody.angularVelocity).y > 0f;

	private void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
		PlayerBody = GetComponent<PlayerBodyV2>();
		Hover = GetComponent<Hover>();
	}

	private void Start()
	{
		currentMaxSpeed = maxForwardsSpeed;
		currentAcceleration = forwardsAcceleration;
	}

	private void FixedUpdate()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Move();
			Turn();
		}
	}

	private void Move()
	{
		if (!Hover.IsGrounded)
		{
			return;
		}
		if (IsMovingForwards)
		{
			if (Sprint)
			{
				currentMaxSpeed = maxForwardsSprintSpeed;
				currentAcceleration = ((Speed < maxForwardsSpeed) ? forwardsSprintAcceleration : forwardsSprintOverspeedAcceleration);
			}
			else
			{
				currentMaxSpeed = maxForwardsSpeed;
				currentAcceleration = forwardsAcceleration;
			}
		}
		else if (IsMovingBackwards)
		{
			if (Sprint)
			{
				currentMaxSpeed = maxBackwardsSprintSpeed;
				currentAcceleration = ((Speed < maxForwardsSpeed) ? backwardsSprintAcceleration : backwardsSprintOverspeedAcceleration);
			}
			else
			{
				currentMaxSpeed = maxBackwardsSpeed;
				currentAcceleration = backwardsAcceleration;
			}
		}
		if (MoveForwards)
		{
			if (IsMovingForwards)
			{
				float num = ((Speed < currentMaxSpeed) ? currentAcceleration : 0f);
				Rigidbody.AddForce(MovementDirection.forward * num, ForceMode.Acceleration);
			}
			else if (IsMovingBackwards)
			{
				float num2 = brakeAcceleration;
				Rigidbody.AddForce(MovementDirection.forward * num2, ForceMode.Acceleration);
			}
		}
		else if (MoveBackwards)
		{
			if (IsMovingBackwards)
			{
				float num3 = ((Speed < currentMaxSpeed) ? currentAcceleration : 0f);
				Rigidbody.AddForce(-MovementDirection.forward * num3, ForceMode.Acceleration);
			}
			else if (IsMovingForwards)
			{
				float num4 = brakeAcceleration;
				Rigidbody.AddForce(-MovementDirection.forward * num4, ForceMode.Acceleration);
			}
		}
		if (Speed > MaximumSpeed)
		{
			Rigidbody.linearVelocity *= 1f - overspeedDrag * Time.fixedDeltaTime;
		}
		else
		{
			Rigidbody.linearVelocity *= 1f - drag * Time.fixedDeltaTime;
		}
		Rigidbody.linearVelocity *= 1f - AmbientDrag * Time.fixedDeltaTime;
	}

	private void Turn()
	{
		if (TurnLeft)
		{
			if (IsTurningLeft)
			{
				float num = ((TurnSpeed < turnMaxSpeed * TurnMultiplier) ? turnAcceleration : 0f);
				Rigidbody.AddTorque(base.transform.up * (0f - num) * TurnMultiplier, ForceMode.Acceleration);
			}
			else if (IsTurningRight)
			{
				float num2 = turnBrakeAcceleration;
				Rigidbody.AddTorque(base.transform.up * (0f - num2) * TurnMultiplier, ForceMode.Acceleration);
			}
		}
		else if (TurnRight)
		{
			if (IsTurningRight)
			{
				float num3 = ((TurnSpeed < turnMaxSpeed * TurnMultiplier) ? turnAcceleration : 0f);
				Rigidbody.AddTorque(base.transform.up * num3 * TurnMultiplier, ForceMode.Acceleration);
			}
			else if (IsTurningLeft)
			{
				float num4 = turnBrakeAcceleration;
				Rigidbody.AddTorque(base.transform.up * num4 * TurnMultiplier, ForceMode.Acceleration);
			}
		}
		else if (TurnSpeed < turnMaxSpeed * TurnMultiplier)
		{
			Rigidbody.angularVelocity *= 1f - turnDrag * Time.fixedDeltaTime;
		}
		if (TurnSpeed > turnMaxSpeed * TurnMultiplier)
		{
			Rigidbody.angularVelocity *= 1f - turnOverspeedDrag * Time.fixedDeltaTime;
		}
	}
}
