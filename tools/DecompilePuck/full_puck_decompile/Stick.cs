using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

public class Stick : NetworkBehaviour
{
	[Header("References")]
	[SerializeField]
	private GameObject shaftHandle;

	[SerializeField]
	private GameObject bladeHandle;

	[SerializeField]
	private GameObject rotationContainer;

	[SerializeField]
	private StickMesh stickMesh;

	[Header("Settings")]
	[SerializeField]
	private float bladeAngleStep = 12.5f;

	[Space(20f)]
	[SerializeField]
	private bool transferAngularVelocity = true;

	[SerializeField]
	private float angularVelocityTransferMultiplier = 0.25f;

	[Space(20f)]
	[SerializeField]
	private float shaftHandleProportionalGain = 500f;

	[SerializeField]
	private float shaftHandleIntegralGain;

	[SerializeField]
	private float shaftHandleIntegralSaturation;

	[SerializeField]
	private float shaftHandleDerivativeGain = 20f;

	[SerializeField]
	private float shaftHandleDerivativeSmoothing = 0.1f;

	[SerializeField]
	private float minShaftHandleProportionalGainMultiplier = 0.25f;

	[Space(20f)]
	[SerializeField]
	private float bladeHandleProportionalGain = 500f;

	[SerializeField]
	private float bladeHandleIntegralGain;

	[SerializeField]
	private float bladeHandleIntegralSaturation;

	[SerializeField]
	private float bladeHandleDerivativeGain = 20f;

	[SerializeField]
	private float bladeHandleDerivativeSmoothing = 0.1f;

	[Space(20f)]
	[SerializeField]
	private float linearVelocityTransferMultiplier = 0.25f;

	[HideInInspector]
	public NetworkVariable<NetworkObjectReference> PlayerReference = new NetworkVariable<NetworkObjectReference>();

	[HideInInspector]
	public Player Player;

	[HideInInspector]
	public Rigidbody Rigidbody;

	[HideInInspector]
	public NetworkObjectCollisionBuffer NetworkObjectCollisionBuffer;

	[HideInInspector]
	public float Length;

	private Vector3PIDController shaftHandlePIDController = new Vector3PIDController();

	private Vector3PIDController bladeHandlePIDController = new Vector3PIDController();

	private float shaftHandleProportionalGainMultiplier = 1f;

	private float bladeHandleProportionalGainMultiplier = 1f;

	[HideInInspector]
	public PlayerBodyV2 PlayerBody => Player.PlayerBody;

	[HideInInspector]
	public StickPositioner StickPositioner => Player.StickPositioner;

	[HideInInspector]
	public StickMesh StickMesh => stickMesh;

	[HideInInspector]
	public Vector3 ShaftHandleLocalPosition => shaftHandle.transform.localPosition;

	[HideInInspector]
	public Vector3 BladeHandleLocalPosition => bladeHandle.transform.localPosition;

	[HideInInspector]
	public Vector3 ShaftHandlePosition => shaftHandle.transform.position;

	[HideInInspector]
	public Vector3 BladeHandlePosition => bladeHandle.transform.position;

	private void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
		NetworkObjectCollisionBuffer = GetComponent<NetworkObjectCollisionBuffer>();
		Length = Vector3.Distance(ShaftHandlePosition, BladeHandlePosition);
	}

	protected override void OnNetworkPostSpawn()
	{
		if (PlayerReference.Value.TryGet(out var networkObject))
		{
			Player = networkObject.GetComponent<Player>();
		}
		if ((bool)Player)
		{
			Player.Stick = this;
			UpdateStick();
			if (Player.IsReplay.Value)
			{
				Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
				Rigidbody.interpolation = RigidbodyInterpolation.None;
			}
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnStickSpawned", new Dictionary<string, object> { { "stick", this } });
		base.OnNetworkPostSpawn();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnStickDespawned", new Dictionary<string, object> { { "stick", this } });
		base.OnNetworkDespawn();
	}

	public override void OnDestroy()
	{
		base.transform.DOKill();
		base.OnDestroy();
	}

	private void FixedUpdate()
	{
		if (!Player || !StickPositioner)
		{
			return;
		}
		PlayerInput playerInput = Player.PlayerInput;
		if (!playerInput)
		{
			return;
		}
		float angle = (float)playerInput.BladeAngleInput.ServerValue * bladeAngleStep;
		rotationContainer.transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
		if (NetworkManager.Singleton.IsServer)
		{
			shaftHandlePIDController.proportionalGain = shaftHandleProportionalGain * shaftHandleProportionalGainMultiplier;
			shaftHandlePIDController.integralGain = shaftHandleIntegralGain;
			shaftHandlePIDController.integralSaturation = shaftHandleIntegralSaturation;
			shaftHandlePIDController.derivativeGain = shaftHandleDerivativeGain;
			shaftHandlePIDController.derivativeSmoothing = shaftHandleDerivativeSmoothing;
			bladeHandlePIDController.proportionalGain = bladeHandleProportionalGain * bladeHandleProportionalGainMultiplier;
			bladeHandlePIDController.integralGain = bladeHandleIntegralGain;
			bladeHandlePIDController.integralSaturation = bladeHandleIntegralSaturation;
			bladeHandlePIDController.derivativeGain = bladeHandleDerivativeGain;
			bladeHandlePIDController.derivativeSmoothing = bladeHandleDerivativeSmoothing;
			Vector3 vector = shaftHandlePIDController.Update(Time.fixedDeltaTime, ShaftHandlePosition, StickPositioner.ShaftTargetPosition);
			Vector3 vector2 = bladeHandlePIDController.Update(Time.fixedDeltaTime, BladeHandlePosition, StickPositioner.BladeTargetPosition);
			Rigidbody.AddForceAtPosition(PlayerBody.Rigidbody.GetPointVelocity(shaftHandle.transform.position) * linearVelocityTransferMultiplier * Time.fixedDeltaTime, shaftHandle.transform.position, ForceMode.VelocityChange);
			Rigidbody.AddForceAtPosition(PlayerBody.Rigidbody.GetPointVelocity(bladeHandle.transform.position) * linearVelocityTransferMultiplier * Time.fixedDeltaTime, bladeHandle.transform.position, ForceMode.VelocityChange);
			Rigidbody.AddForceAtPosition(vector * Time.fixedDeltaTime, ShaftHandlePosition, ForceMode.VelocityChange);
			Rigidbody.AddForceAtPosition(vector2 * Time.fixedDeltaTime, BladeHandlePosition, ForceMode.VelocityChange);
			Vector3 direction = base.transform.InverseTransformVector(Rigidbody.angularVelocity);
			direction.z = 0f;
			Rigidbody.angularVelocity = base.transform.TransformDirection(direction);
			Vector3 vector3 = Utils.WrapEulerAngles(base.transform.eulerAngles);
			Quaternion rot = Quaternion.Euler(new Vector3(vector3.x, vector3.y, 0f));
			Rigidbody.MoveRotation(rot);
			Vector3 vector4 = Vector3.Scale(Rigidbody.angularVelocity, new Vector3(0.5f, 1f, 0f)) * angularVelocityTransferMultiplier;
			if (transferAngularVelocity)
			{
				PlayerBody.Rigidbody.AddTorque(-vector4, ForceMode.Acceleration);
			}
			bladeHandleProportionalGainMultiplier = 1f;
		}
	}

	public void Teleport(Vector3 position, Quaternion rotation)
	{
		base.transform.position = position;
		base.transform.rotation = rotation;
		Rigidbody.position = position;
		Rigidbody.rotation = rotation;
		Rigidbody.linearVelocity = Vector3.zero;
		Rigidbody.angularVelocity = Vector3.zero;
		shaftHandlePIDController.Reset();
		bladeHandlePIDController.Reset();
	}

	public void Server_Freeze()
	{
		if (NetworkManager.Singleton.IsServer && !Player.IsReplay.Value)
		{
			Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
		}
	}

	public void Server_Unfreeze()
	{
		if (NetworkManager.Singleton.IsServer && !Player.IsReplay.Value)
		{
			Rigidbody.constraints = RigidbodyConstraints.None;
		}
	}

	public void UpdateStick()
	{
		if ((bool)Player)
		{
			stickMesh.SetSkin(Player.Team.Value, Player.GetPlayerStickSkin().ToString());
			stickMesh.SetShaftTape(Player.GetPlayerStickShaftTapeSkin().ToString());
			stickMesh.SetBladeTape(Player.GetPlayerStickBladeTapeSkin().ToString());
		}
	}

	private void OnCollisionStay(Collision collision)
	{
		Stick component = collision.gameObject.GetComponent<Stick>();
		if ((bool)component && collision.contacts.Length != 0)
		{
			ContactPoint contactPoint = collision.contacts[0];
			Collider thisCollider = contactPoint.thisCollider;
			Collider otherCollider = contactPoint.otherCollider;
			if (!(thisCollider.tag != "Stick Blade") && !(otherCollider.tag != "Stick Shaft"))
			{
				Vector3 point = contactPoint.point;
				float num = Mathf.Clamp(Vector3.Distance(component.ShaftHandlePosition, point) / Length, minShaftHandleProportionalGainMultiplier, 1f);
				bladeHandleProportionalGainMultiplier = num;
			}
		}
	}

	protected override void __initializeVariables()
	{
		if (PlayerReference == null)
		{
			throw new Exception("Stick.PlayerReference cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		PlayerReference.Initialize(this);
		__nameNetworkVariable(PlayerReference, "PlayerReference");
		NetworkVariableFields.Add(PlayerReference);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		base.__initializeRpcs();
	}

	protected internal override string __getTypeName()
	{
		return "Stick";
	}
}
