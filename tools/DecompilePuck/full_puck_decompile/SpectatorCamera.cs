using System;
using Unity.Netcode;
using UnityEngine;

public class SpectatorCamera : BaseCamera
{
	[Header("Settings")]
	[SerializeField]
	private float freeLookMovementSpeed = 5f;

	[SerializeField]
	private float freeLookPositionSmoothing = 1f;

	[SerializeField]
	private float freeLookRotationSmoothing = 1f;

	private Vector3 freeLookPosition = Vector3.up * 2f;

	private Vector3 freeLookAngle = Vector3.zero;

	private Quaternion freeLookRotation = Quaternion.identity;

	[HideInInspector]
	public NetworkVariable<NetworkObjectReference> PlayerReference = new NetworkVariable<NetworkObjectReference>();

	[HideInInspector]
	public Player Player;

	protected override void OnNetworkPostSpawn()
	{
		if (PlayerReference.Value.TryGet(out var networkObject))
		{
			Player = networkObject.GetComponent<Player>();
		}
		if ((bool)Player)
		{
			Player.SpectatorCamera = this;
		}
		if ((bool)Player && Player.IsLocalPlayer)
		{
			Enable();
		}
		base.OnNetworkPostSpawn();
	}

	public override void OnTick(float deltaTime)
	{
		base.OnTick(deltaTime);
		if (base.IsOwner && !NetworkBehaviourSingleton<UIManager>.Instance.isMouseActive)
		{
			Vector3 vector = new Vector3((MonoBehaviourSingleton<InputManager>.Instance.TurnRightAction.IsPressed() ? 1 : 0) + (MonoBehaviourSingleton<InputManager>.Instance.TurnLeftAction.IsPressed() ? (-1) : 0), (MonoBehaviourSingleton<InputManager>.Instance.MoveForwardAction.IsPressed() ? 1 : 0) + (MonoBehaviourSingleton<InputManager>.Instance.MoveBackwardAction.IsPressed() ? (-1) : 0), MonoBehaviourSingleton<InputManager>.Instance.JumpAction.IsPressed() ? 1 : (MonoBehaviourSingleton<InputManager>.Instance.SlideAction.IsPressed() ? (-1) : 0));
			float num = (MonoBehaviourSingleton<InputManager>.Instance.SprintAction.IsPressed() ? (freeLookMovementSpeed * 2f) : freeLookMovementSpeed);
			freeLookPosition += base.transform.right * vector.x * deltaTime * num;
			freeLookPosition += base.transform.forward * vector.y * deltaTime * num;
			freeLookPosition += base.transform.up * vector.z * deltaTime * num;
			Vector2 vector2 = MonoBehaviourSingleton<InputManager>.Instance.StickAction.ReadValue<Vector2>();
			float lookSensitivity = MonoBehaviourSingleton<SettingsManager>.Instance.LookSensitivity;
			freeLookAngle += new Vector3((0f - vector2.y) * lookSensitivity, vector2.x * lookSensitivity, 0f - freeLookRotation.eulerAngles.z);
			freeLookAngle.x = Mathf.Clamp(freeLookAngle.x, -80f, 80f);
			freeLookRotation = Quaternion.Euler(Utils.WrapEulerAngles(freeLookAngle));
			base.transform.position = Vector3.Lerp(base.transform.position, freeLookPosition, deltaTime / freeLookPositionSmoothing);
			base.transform.rotation = Quaternion.Lerp(base.transform.rotation, freeLookRotation, deltaTime / freeLookRotationSmoothing);
		}
	}

	protected override void __initializeVariables()
	{
		if (PlayerReference == null)
		{
			throw new Exception("SpectatorCamera.PlayerReference cannot be null. All NetworkVariableBase instances must be initialized.");
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
		return "SpectatorCamera";
	}
}
