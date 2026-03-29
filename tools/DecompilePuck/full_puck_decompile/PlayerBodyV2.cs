using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBodyV2 : NetworkBehaviour
{
	[Header("References")]
	[SerializeField]
	private Transform movementDirection;

	[SerializeField]
	private PlayerMesh playerMesh;

	[SerializeField]
	private SynchronizedAudio windAudioSource;

	[SerializeField]
	private SynchronizedAudio iceAudioSource;

	[SerializeField]
	private SynchronizedAudio gruntAudioSource;

	[SerializeField]
	private AudioSource voiceAudioSource;

	[Header("Settings")]
	[SerializeField]
	private float gravityMultiplier = 2f;

	[SerializeField]
	private float hoverDistance = 1.2f;

	[Space(20f)]
	[SerializeField]
	private float upwardnessThreshold = 0.8f;

	[SerializeField]
	private float sidewaysThreshold = 0.2f;

	[Space(20f)]
	[SerializeField]
	private float balanceLossTime = 0.25f;

	[SerializeField]
	private float balanceRecoveryTime = 5f;

	[Space(20f)]
	[SerializeField]
	private float staminaRegenerationRate = 10f;

	[Space(20f)]
	[SerializeField]
	private float sprintStaminaDrainRate = 1.4f;

	[Space(20f)]
	[SerializeField]
	private float slideTurnMultiplier = 2f;

	[SerializeField]
	private float slideHoverDistance = 0.8f;

	[Space(20f)]
	[SerializeField]
	private float jumpVelocity = 6f;

	[SerializeField]
	private float jumpStaminaDrain = 0.125f;

	[SerializeField]
	private float jumpTurnMultiplier = 5f;

	[Space(20f)]
	[SerializeField]
	private float twistVelocity = 5f;

	[SerializeField]
	private float twistStaminaDrain = 0.125f;

	[Space(20f)]
	[SerializeField]
	private bool canDash = true;

	[SerializeField]
	private float dashVelocity = 6f;

	[SerializeField]
	private float dashStaminaDrain = 0.125f;

	[SerializeField]
	private float dashDrag = 5f;

	[SerializeField]
	private float dashDragTime = 1f;

	[Space(20f)]
	[SerializeField]
	private float slideDrag = 0.2f;

	[SerializeField]
	private float stopDrag = 2.5f;

	[SerializeField]
	private float fallenDrag = 0.2f;

	[Space(20f)]
	[SerializeField]
	private float tackleSpeedThreshold = 7.6f;

	[SerializeField]
	private float tackleForceThreshold = 7f;

	[SerializeField]
	private float tackleForceMultiplier = 0.3f;

	[SerializeField]
	private float tackleBounceMaximumMagnitude = 10f;

	[Space(20f)]
	[SerializeField]
	private float stretchSpeed = 10f;

	[Space(20f)]
	[SerializeField]
	private float maximumLaterality = 1f;

	[SerializeField]
	private float minimumLaterality = 0.5f;

	[SerializeField]
	private float minimumLateralitySpeed = 2f;

	[SerializeField]
	private float maximumLateralitySpeed = 5f;

	[Space(20f)]
	[SerializeField]
	private AnimationCurve windVolumeCurve;

	[SerializeField]
	private AnimationCurve iceVolumeCurve;

	[SerializeField]
	private AnimationCurve icePitchCurve;

	[SerializeField]
	private AnimationCurve gruntVolumeCurve;

	[SerializeField]
	private AnimationCurve gruntPitchCurve;

	[HideInInspector]
	public NetworkVariable<NetworkObjectReference> PlayerReference = new NetworkVariable<NetworkObjectReference>();

	[HideInInspector]
	public NetworkVariable<short> StaminaCompressed = new NetworkVariable<short>(0);

	[HideInInspector]
	public NetworkVariable<short> SpeedCompressed = new NetworkVariable<short>(0);

	[HideInInspector]
	public NetworkVariable<bool> IsSprinting = new NetworkVariable<bool>(value: false);

	[HideInInspector]
	public NetworkVariable<bool> IsSliding = new NetworkVariable<bool>(value: false);

	[HideInInspector]
	public NetworkVariable<bool> IsStopping = new NetworkVariable<bool>(value: false);

	[HideInInspector]
	public NetworkVariable<bool> IsExtendedLeft = new NetworkVariable<bool>(value: false);

	[HideInInspector]
	public NetworkVariable<bool> IsExtendedRight = new NetworkVariable<bool>(value: false);

	[HideInInspector]
	public Player Player;

	[HideInInspector]
	public Rigidbody Rigidbody;

	[HideInInspector]
	public Movement Movement;

	[HideInInspector]
	public VelocityLean VelocityLean;

	[HideInInspector]
	public Hover Hover;

	[HideInInspector]
	public Skate Skate;

	[HideInInspector]
	public KeepUpright KeepUpright;

	[HideInInspector]
	public MeshRendererHider MeshRendererHider;

	[HideInInspector]
	public CollisionRecorder CollisionRecorder;

	[HideInInspector]
	public bool HasDashed;

	[HideInInspector]
	public bool HasDashExtended;

	[HideInInspector]
	public bool HasSlipped;

	[HideInInspector]
	public bool HasFallen;

	[HideInInspector]
	public float Laterality;

	private Tween balanceLossTween;

	private Tween balanceRecoveryTween;

	private Tween dashDragTween;

	private Tween dashLegPadTween;

	[HideInInspector]
	public float Stamina
	{
		get
		{
			return (float)StaminaCompressed.Value / 16383f;
		}
		set
		{
			StaminaCompressed.Value = (short)(value * 16383f);
		}
	}

	[HideInInspector]
	public float Speed
	{
		get
		{
			return (float)SpeedCompressed.Value / 327f;
		}
		set
		{
			SpeedCompressed.Value = (short)(value * 327f);
		}
	}

	[HideInInspector]
	public PlayerCamera PlayerCamera => Player.PlayerCamera;

	[HideInInspector]
	public Stick Stick => Player.Stick;

	[HideInInspector]
	public PlayerMesh PlayerMesh => playerMesh;

	[HideInInspector]
	public AudioSource VoiceAudioSource => voiceAudioSource;

	[HideInInspector]
	public float Upwardness => Vector3.Dot(base.transform.up, Vector3.up);

	[HideInInspector]
	public bool IsUpright => Upwardness > upwardnessThreshold;

	[HideInInspector]
	public bool IsSlipping => Upwardness < upwardnessThreshold;

	[HideInInspector]
	public bool IsSideways => Upwardness < sidewaysThreshold;

	[HideInInspector]
	public bool IsGrounded
	{
		get
		{
			if (Hover.IsGrounded)
			{
				return IsUpright;
			}
			return false;
		}
	}

	[HideInInspector]
	public bool IsJumping
	{
		get
		{
			if (!Hover.IsGrounded)
			{
				return IsUpright;
			}
			return false;
		}
	}

	[HideInInspector]
	public bool IsBalanced => KeepUpright.Balance >= 1f;

	[HideInInspector]
	public Transform MovementDirection => movementDirection;

	public virtual void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
		MeshRendererHider = GetComponent<MeshRendererHider>();
		CollisionRecorder = GetComponent<CollisionRecorder>();
		CollisionRecorder collisionRecorder = CollisionRecorder;
		collisionRecorder.OnDeferredCollision = (Action<GameObject, float>)Delegate.Combine(collisionRecorder.OnDeferredCollision, new Action<GameObject, float>(Server_OnDeferredCollision));
		Movement = GetComponent<Movement>();
		Movement.MovementDirection = MovementDirection;
		VelocityLean = GetComponent<VelocityLean>();
		VelocityLean.MovementDirection = MovementDirection;
		Hover = GetComponent<Hover>();
		Skate = GetComponent<Skate>();
		Skate.MovementDirection = MovementDirection;
		KeepUpright = GetComponent<KeepUpright>();
	}

	public override void OnNetworkSpawn()
	{
		MonoBehaviourSingleton<InputManager>.Instance.DebugTackleAction.performed += OnDebugTackleActionPerformed;
		StaminaCompressed.Initialize(this);
		NetworkVariable<short> staminaCompressed = StaminaCompressed;
		staminaCompressed.OnValueChanged = (NetworkVariable<short>.OnValueChangedDelegate)Delegate.Combine(staminaCompressed.OnValueChanged, new NetworkVariable<short>.OnValueChangedDelegate(OnStaminaChanged));
		SpeedCompressed.Initialize(this);
		NetworkVariable<short> speedCompressed = SpeedCompressed;
		speedCompressed.OnValueChanged = (NetworkVariable<short>.OnValueChangedDelegate)Delegate.Combine(speedCompressed.OnValueChanged, new NetworkVariable<short>.OnValueChangedDelegate(OnSpeedChanged));
		IsSprinting.Initialize(this);
		NetworkVariable<bool> isSprinting = IsSprinting;
		isSprinting.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isSprinting.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnIsSprintingChanged));
		IsSliding.Initialize(this);
		NetworkVariable<bool> isSliding = IsSliding;
		isSliding.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isSliding.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnIsSlidingChanged));
		IsStopping.Initialize(this);
		NetworkVariable<bool> isStopping = IsStopping;
		isStopping.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isStopping.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnIsStoppingChanged));
		IsExtendedLeft.Initialize(this);
		NetworkVariable<bool> isExtendedLeft = IsExtendedLeft;
		isExtendedLeft.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isExtendedLeft.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnIsExtendedLeftChanged));
		IsExtendedRight.Initialize(this);
		NetworkVariable<bool> isExtendedRight = IsExtendedRight;
		isExtendedRight.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isExtendedRight.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnIsExtendedRightChanged));
		Client_InitializeNetworkVariables();
		if (NetworkManager.Singleton.IsServer)
		{
			Stamina = 1f;
		}
		base.OnNetworkSpawn();
	}

	protected override void OnNetworkPostSpawn()
	{
		if (PlayerReference.Value.TryGet(out var networkObject))
		{
			Player = networkObject.GetComponent<Player>();
		}
		if ((bool)Player)
		{
			Player.PlayerBody = this;
		}
		if ((bool)Player && Player.IsReplay.Value)
		{
			Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
			Rigidbody.interpolation = RigidbodyInterpolation.None;
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerBodySpawned", new Dictionary<string, object> { { "playerBody", this } });
		base.OnNetworkPostSpawn();
	}

	protected override void OnNetworkSessionSynchronized()
	{
		Client_InitializeNetworkVariables();
		base.OnNetworkSessionSynchronized();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<InputManager>.Instance.DebugTackleAction.performed -= OnDebugTackleActionPerformed;
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerBodyDespawned", new Dictionary<string, object> { { "playerBody", this } });
		NetworkVariable<short> staminaCompressed = StaminaCompressed;
		staminaCompressed.OnValueChanged = (NetworkVariable<short>.OnValueChangedDelegate)Delegate.Remove(staminaCompressed.OnValueChanged, new NetworkVariable<short>.OnValueChangedDelegate(OnStaminaChanged));
		StaminaCompressed.Dispose();
		NetworkVariable<short> speedCompressed = SpeedCompressed;
		speedCompressed.OnValueChanged = (NetworkVariable<short>.OnValueChangedDelegate)Delegate.Remove(speedCompressed.OnValueChanged, new NetworkVariable<short>.OnValueChangedDelegate(OnSpeedChanged));
		SpeedCompressed.Dispose();
		NetworkVariable<bool> isSprinting = IsSprinting;
		isSprinting.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isSprinting.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnIsSprintingChanged));
		IsSprinting.Dispose();
		NetworkVariable<bool> isSliding = IsSliding;
		isSliding.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isSliding.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnIsSlidingChanged));
		IsSliding.Dispose();
		NetworkVariable<bool> isStopping = IsStopping;
		isStopping.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isStopping.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnIsStoppingChanged));
		IsStopping.Dispose();
		NetworkVariable<bool> isExtendedLeft = IsExtendedLeft;
		isExtendedLeft.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isExtendedLeft.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnIsExtendedLeftChanged));
		IsExtendedLeft.Dispose();
		NetworkVariable<bool> isExtendedRight = IsExtendedRight;
		isExtendedRight.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isExtendedRight.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnIsExtendedRightChanged));
		IsExtendedRight.Dispose();
		base.OnNetworkDespawn();
	}

	public override void OnDestroy()
	{
		balanceLossTween?.Kill();
		balanceRecoveryTween?.Kill();
		dashDragTween?.Kill();
		dashLegPadTween?.Kill();
		CollisionRecorder collisionRecorder = CollisionRecorder;
		collisionRecorder.OnDeferredCollision = (Action<GameObject, float>)Delegate.Remove(collisionRecorder.OnDeferredCollision, new Action<GameObject, float>(Server_OnDeferredCollision));
		base.OnDestroy();
	}

	public virtual void FixedUpdate()
	{
		if (!Player)
		{
			return;
		}
		PlayerInput playerInput = Player.PlayerInput;
		if (!playerInput)
		{
			return;
		}
		if (playerInput.LookInput.ServerValue || playerInput.TrackInput.ServerValue)
		{
			if ((bool)PlayerCamera)
			{
				playerMesh.LookAt(PlayerCamera.transform.position + PlayerCamera.transform.forward * 10f, Time.fixedDeltaTime);
			}
		}
		else if ((bool)Stick)
		{
			playerMesh.LookAt(Stick.BladeHandlePosition, Time.fixedDeltaTime);
		}
		float b = (IsJumping ? 1.05f : (IsSliding.Value ? 0.95f : 1f));
		PlayerMesh.Stretch = Mathf.Lerp(PlayerMesh.Stretch, b, Time.fixedDeltaTime * stretchSpeed);
		if (NetworkManager.Singleton.IsServer)
		{
			if (!Player.IsReplay.Value)
			{
				HandleInputs(playerInput);
			}
			Speed = Movement.Speed;
			if (IsSprinting.Value)
			{
				Stamina -= (IsSprinting.Value ? (Time.deltaTime / sprintStaminaDrainRate) : 0f);
			}
			else if (Stamina < 1f)
			{
				Stamina += Time.fixedDeltaTime / staminaRegenerationRate;
				Stamina = Mathf.Clamp(Stamina, 0f, 1f);
			}
			if (IsUpright)
			{
				Rigidbody.AddForce(Vector3.up * (0f - Physics.gravity.y), ForceMode.Acceleration);
				Rigidbody.AddForce(Vector3.down * (0f - Physics.gravity.y) * gravityMultiplier, ForceMode.Acceleration);
			}
			MovementDirection.localRotation = Quaternion.FromToRotation(base.transform.forward, Utils.Vector3Slerp3(-base.transform.right, base.transform.forward, base.transform.right, Laterality));
			Movement.Sprint = IsSprinting.Value;
			Movement.TurnMultiplier = (IsSliding.Value ? slideTurnMultiplier : (IsJumping ? jumpTurnMultiplier : 1f));
			Movement.AmbientDrag = (HasFallen ? fallenDrag : (HasDashed ? Movement.AmbientDrag : (IsStopping.Value ? stopDrag : (IsSliding.Value ? slideDrag : 0f))));
			Hover.TargetDistance = (IsSliding.Value ? slideHoverDistance : (KeepUpright.Balance * hoverDistance));
			Skate.Intensity = ((IsSliding.Value || IsStopping.Value || !IsGrounded) ? 0f : KeepUpright.Balance);
			VelocityLean.AngularIntensity = Mathf.Max(0.1f, Movement.NormalizedMaximumSpeed) / (IsSliding.Value ? 2f : (IsJumping ? 2f : 1f));
			VelocityLean.Inverted = !IsJumping && !IsSliding.Value && Movement.IsMovingBackwards;
			VelocityLean.UseWorldLinearVelocity = IsJumping || IsSliding.Value;
			if (!HasSlipped && !HasFallen && IsSlipping)
			{
				OnSlip();
			}
			else if (HasSlipped && !HasFallen && IsSideways)
			{
				OnFall();
			}
			else if (HasFallen && !HasSlipped && IsUpright)
			{
				OnStandUp();
			}
			Server_UpdateAudio();
		}
	}

	private void HandleInputs(PlayerInput playerInput)
	{
		if (!IsSprinting.Value && playerInput.SprintInput.ServerValue && !IsSliding.Value && IsGrounded && Stamina > 0.25f)
		{
			IsSprinting.Value = true;
		}
		else if (IsSprinting.Value && !playerInput.SprintInput.ServerValue)
		{
			IsSprinting.Value = false;
		}
		else if (IsSprinting.Value)
		{
			IsSprinting.Value = !IsSliding.Value && IsGrounded && Stamina > 0f;
		}
		IsSliding.Value = playerInput.SlideInput.ServerValue && IsGrounded;
		IsStopping.Value = playerInput.StopInput.ServerValue && IsGrounded;
		if (!HasDashExtended)
		{
			IsExtendedLeft.Value = playerInput.ExtendLeftInput.ServerValue && IsGrounded && IsSliding.Value;
			IsExtendedRight.Value = playerInput.ExtendRightInput.ServerValue && IsGrounded && IsSliding.Value;
		}
		Movement.MoveForwards = !IsSliding.Value && playerInput.MoveInput.ServerValue.y > 0f;
		Movement.MoveBackwards = !IsSliding.Value && playerInput.MoveInput.ServerValue.y < 0f;
		Movement.TurnRight = playerInput.MoveInput.ServerValue.x > 0f;
		Movement.TurnLeft = playerInput.MoveInput.ServerValue.x < 0f;
		float t = Mathf.Clamp01(1f - Movement.NormalizedMinimumSpeed);
		float num = Mathf.Lerp(minimumLateralitySpeed, maximumLateralitySpeed, t);
		float num2 = Mathf.Lerp(minimumLaterality, maximumLaterality, t);
		if (playerInput.LateralLeftInput.ServerValue)
		{
			Laterality = Mathf.Lerp(Laterality, 0f - num2, Time.fixedDeltaTime * num);
		}
		else if (playerInput.LateralRightInput.ServerValue)
		{
			Laterality = Mathf.Lerp(Laterality, num2, Time.fixedDeltaTime * num);
		}
		else
		{
			Laterality = Mathf.Lerp(Laterality, 0f, Time.fixedDeltaTime * num);
		}
	}

	public void OnSlip()
	{
		HasSlipped = true;
		HasFallen = false;
		balanceRecoveryTween?.Kill();
		balanceLossTween?.Kill();
		balanceLossTween = DOTween.To(() => KeepUpright.Balance, delegate(float value)
		{
			KeepUpright.Balance = value;
		}, 0f, balanceLossTime).SetEase(Ease.Linear);
	}

	public void OnFall()
	{
		HasSlipped = false;
		HasFallen = true;
		balanceRecoveryTween?.Kill();
		balanceRecoveryTween = DOTween.To(() => KeepUpright.Balance, delegate(float value)
		{
			KeepUpright.Balance = value;
		}, 1f, balanceRecoveryTime).SetEase(Ease.Linear);
	}

	public void OnStandUp()
	{
		HasSlipped = false;
		HasFallen = false;
	}

	public void UpdateMesh()
	{
		if ((bool)Player)
		{
			playerMesh.SetUsername(Player.Username.Value.ToString());
			playerMesh.SetNumber(Player.Number.Value.ToString());
			PlayerMesh.SetJersey(Player.Team.Value, Player.GetPlayerJerseySkin().ToString());
			PlayerMesh.SetRole(Player.Role.Value);
			PlayerMesh.PlayerHead.SetHelmetFlag(Player.Country.Value.ToString());
			PlayerMesh.PlayerHead.SetHelmetVisor(Player.GetPlayerVisorSkin().ToString());
			PlayerMesh.PlayerHead.SetMustache(Player.Mustache.Value.ToString());
			PlayerMesh.PlayerHead.SetBeard(Player.Beard.Value.ToString());
		}
	}

	public void Jump()
	{
		if (NetworkManager.Singleton.IsServer && IsGrounded && !(Stamina < jumpStaminaDrain))
		{
			Rigidbody.AddForce(Vector3.up * jumpVelocity, ForceMode.VelocityChange);
			Stamina = Mathf.Max(0f, Stamina - jumpStaminaDrain);
		}
	}

	public void TwistLeft()
	{
		if (NetworkManager.Singleton.IsServer && IsJumping && !(Stamina < twistStaminaDrain))
		{
			Rigidbody.AddTorque(-base.transform.up * twistVelocity, ForceMode.VelocityChange);
			Stamina = Mathf.Max(0f, Stamina - twistStaminaDrain);
		}
	}

	public void TwistRight()
	{
		if (NetworkManager.Singleton.IsServer && IsJumping && !(Stamina < twistStaminaDrain))
		{
			Rigidbody.AddTorque(base.transform.up * twistVelocity, ForceMode.VelocityChange);
			Stamina = Mathf.Max(0f, Stamina - twistStaminaDrain);
		}
	}

	public void DashLeft()
	{
		if (NetworkManager.Singleton.IsServer && canDash && IsSliding.Value && !(Stamina < dashStaminaDrain))
		{
			Rigidbody.AddForce(-base.transform.right * dashVelocity, ForceMode.VelocityChange);
			Stamina = Mathf.Max(0f, Stamina - dashStaminaDrain);
			HasDashed = true;
			Movement.AmbientDrag = dashDrag;
			dashDragTween?.Kill();
			dashDragTween = DOTween.To(() => Movement.AmbientDrag, delegate(float value)
			{
				Movement.AmbientDrag = value;
			}, 0f, dashDragTime).OnComplete(delegate
			{
				HasDashed = false;
			}).SetEase(Ease.Linear);
			HasDashExtended = true;
			IsExtendedRight.Value = false;
			IsExtendedRight.Value = true;
			dashLegPadTween?.Kill();
			dashLegPadTween = DOVirtual.DelayedCall(dashDragTime / 4f, delegate
			{
				HasDashExtended = false;
			});
		}
	}

	public void DashRight()
	{
		if (NetworkManager.Singleton.IsServer && canDash && IsSliding.Value && !(Stamina < dashStaminaDrain))
		{
			Rigidbody.AddForce(base.transform.right * dashVelocity, ForceMode.VelocityChange);
			Stamina = Mathf.Max(0f, Stamina - dashStaminaDrain);
			HasDashed = true;
			Movement.AmbientDrag = dashDrag;
			dashDragTween?.Kill();
			dashDragTween = DOTween.To(() => Movement.AmbientDrag, delegate(float value)
			{
				Movement.AmbientDrag = value;
			}, 0f, dashDragTime).OnComplete(delegate
			{
				HasDashed = false;
			}).SetEase(Ease.Linear);
			HasDashExtended = true;
			IsExtendedLeft.Value = false;
			IsExtendedLeft.Value = true;
			dashLegPadTween?.Kill();
			dashLegPadTween = DOVirtual.DelayedCall(dashDragTime / 4f, delegate
			{
				HasDashExtended = false;
			});
		}
	}

	public void CancelDash()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			dashDragTween?.Kill();
			HasDashed = false;
			dashLegPadTween?.Kill();
			HasDashExtended = false;
			IsExtendedLeft.Value = false;
			IsExtendedRight.Value = false;
		}
	}

	public void OnDebugTackleActionPerformed(InputAction.CallbackContext context)
	{
		if (NetworkManager.Singleton.IsServer && (bool)Rigidbody)
		{
			OnSlip();
			Vector3 vector = Vector3.ClampMagnitude(base.transform.forward * 100f, tackleBounceMaximumMagnitude);
			Rigidbody.AddForceAtPosition(-vector * tackleForceMultiplier, Rigidbody.worldCenterOfMass + base.transform.up * 0.5f, ForceMode.VelocityChange);
		}
	}

	private void OnStaminaChanged(short oldStamina, short newStamina)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerBodyStaminaChanged", new Dictionary<string, object>
		{
			{ "playerBody", this },
			{
				"oldStamina",
				(float)oldStamina / 16383f
			},
			{
				"newStamina",
				(float)newStamina / 16383f
			}
		});
	}

	private void OnSpeedChanged(short oldSpeed, short newSpeed)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerBodySpeedChanged", new Dictionary<string, object>
		{
			{ "playerBody", this },
			{
				"oldSpeed",
				(float)oldSpeed / 327f
			},
			{
				"newSpeed",
				(float)newSpeed / 327f
			}
		});
	}

	private void OnIsSprintingChanged(bool oldIsSprinting, bool newIsSprinting)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerBodyIsSprintingChanged", new Dictionary<string, object>
		{
			{ "playerBody", this },
			{ "oldIsSprinting", oldIsSprinting },
			{ "newIsSprinting", newIsSprinting }
		});
	}

	private void OnIsSlidingChanged(bool oldIsSliding, bool newIsSliding)
	{
		PlayerMesh.PlayerLegPadLeft.State = (newIsSliding ? ((!IsExtendedLeft.Value) ? PlayerLegPadState.Butterfly : PlayerLegPadState.ButterflyExtended) : PlayerLegPadState.Idle);
		PlayerMesh.PlayerLegPadRight.State = (newIsSliding ? ((!IsExtendedRight.Value) ? PlayerLegPadState.Butterfly : PlayerLegPadState.ButterflyExtended) : PlayerLegPadState.Idle);
		CancelDash();
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerBodyIsSlidingChanged", new Dictionary<string, object>
		{
			{ "playerBody", this },
			{ "oldIsSliding", oldIsSliding },
			{ "newIsSliding", newIsSliding }
		});
	}

	private void OnIsStoppingChanged(bool oldIsStopping, bool newIsStopping)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerBodyIsStoppingChanged", new Dictionary<string, object>
		{
			{ "playerBody", this },
			{ "oldIsStopping", oldIsStopping },
			{ "newIsStopping", newIsStopping }
		});
	}

	private void OnIsExtendedLeftChanged(bool oldIsExtendedLeft, bool newIsExtendedLeft)
	{
		PlayerMesh.PlayerLegPadLeft.State = (IsSliding.Value ? ((!newIsExtendedLeft) ? PlayerLegPadState.Butterfly : PlayerLegPadState.ButterflyExtended) : PlayerLegPadState.Idle);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerBodyIsExtendedLeftChanged", new Dictionary<string, object>
		{
			{ "playerBody", this },
			{ "oldIsExtendedLeft", oldIsExtendedLeft },
			{ "newIsExtendedLeft", newIsExtendedLeft }
		});
	}

	private void OnIsExtendedRightChanged(bool oldIsExtendedRight, bool newIsExtendedRight)
	{
		PlayerMesh.PlayerLegPadRight.State = (IsSliding.Value ? ((!newIsExtendedRight) ? PlayerLegPadState.Butterfly : PlayerLegPadState.ButterflyExtended) : PlayerLegPadState.Idle);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerBodyIsExtendedRightChanged", new Dictionary<string, object>
		{
			{ "playerBody", this },
			{ "oldIsExtendedRight", oldIsExtendedRight },
			{ "newIsExtendedRight", newIsExtendedRight }
		});
	}

	public void Server_Teleport(Vector3 position, Quaternion rotation)
	{
		base.transform.position = position;
		base.transform.rotation = rotation;
		Rigidbody.position = position;
		Rigidbody.rotation = rotation;
		Rigidbody.linearVelocity = Vector3.zero;
		Rigidbody.angularVelocity = Vector3.zero;
		Stick.Teleport(position, rotation);
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

	private void Server_UpdateAudio()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			float time = Mathf.Min(Movement.NormalizedMaximumSpeed, 1f);
			windAudioSource.Server_SetVolume(windVolumeCurve.Evaluate(time));
			float num = ((!IsGrounded) ? 0f : (IsStopping.Value ? 3f : (IsSliding.Value ? 1.5f : (Skate.IsTractionLost ? 2f : 1f))));
			float num2 = (IsStopping.Value ? 3f : (IsSliding.Value ? 1.5f : (Skate.IsTractionLost ? 2f : 1f)));
			float time2 = Mathf.Min(Movement.NormalizedMaximumSpeed, 1f);
			iceAudioSource.Server_SetVolume(iceVolumeCurve.Evaluate(time2) * num);
			float time3 = Mathf.Min(Movement.NormalizedMaximumSpeed, 1f);
			iceAudioSource.Server_SetPitch(icePitchCurve.Evaluate(time3) * num2);
		}
	}

	private void Server_OnDeferredCollision(GameObject gameObject, float force)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			gruntAudioSource.Server_Play(gruntVolumeCurve.Evaluate(force), gruntPitchCurve.Evaluate(force), isOneShot: true, -1, 0f, randomClip: true);
		}
	}

	public void Client_InitializeNetworkVariables()
	{
		OnStaminaChanged(StaminaCompressed.Value, StaminaCompressed.Value);
		OnSpeedChanged(SpeedCompressed.Value, SpeedCompressed.Value);
		OnIsSprintingChanged(IsSprinting.Value, IsSprinting.Value);
		OnIsSlidingChanged(IsSliding.Value, IsSliding.Value);
		OnIsStoppingChanged(IsStopping.Value, IsStopping.Value);
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (!NetworkManager.Singleton.IsServer || collision.gameObject.layer != LayerMask.NameToLayer("Player"))
		{
			return;
		}
		PlayerBodyV2 component = collision.gameObject.GetComponent<PlayerBodyV2>();
		if ((bool)component)
		{
			float normalizedMaximumSpeed = Movement.NormalizedMaximumSpeed;
			float normalizedMaximumSpeed2 = component.Movement.NormalizedMaximumSpeed;
			float collisionForce = Utils.GetCollisionForce(collision);
			if (!(Speed < tackleSpeedThreshold) && !(normalizedMaximumSpeed < normalizedMaximumSpeed2) && !(collisionForce < tackleForceThreshold) && !IsGrounded && IsBalanced && !HasSlipped && !HasFallen)
			{
				component.OnSlip();
				Vector3 vector = Vector3.ClampMagnitude(collision.relativeVelocity, tackleBounceMaximumMagnitude);
				component.Rigidbody.AddForceAtPosition(-vector * tackleForceMultiplier, Rigidbody.worldCenterOfMass + base.transform.up * 0.5f, ForceMode.VelocityChange);
			}
		}
	}

	protected override void __initializeVariables()
	{
		if (PlayerReference == null)
		{
			throw new Exception("PlayerBodyV2.PlayerReference cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		PlayerReference.Initialize(this);
		__nameNetworkVariable(PlayerReference, "PlayerReference");
		NetworkVariableFields.Add(PlayerReference);
		if (StaminaCompressed == null)
		{
			throw new Exception("PlayerBodyV2.StaminaCompressed cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		StaminaCompressed.Initialize(this);
		__nameNetworkVariable(StaminaCompressed, "StaminaCompressed");
		NetworkVariableFields.Add(StaminaCompressed);
		if (SpeedCompressed == null)
		{
			throw new Exception("PlayerBodyV2.SpeedCompressed cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		SpeedCompressed.Initialize(this);
		__nameNetworkVariable(SpeedCompressed, "SpeedCompressed");
		NetworkVariableFields.Add(SpeedCompressed);
		if (IsSprinting == null)
		{
			throw new Exception("PlayerBodyV2.IsSprinting cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		IsSprinting.Initialize(this);
		__nameNetworkVariable(IsSprinting, "IsSprinting");
		NetworkVariableFields.Add(IsSprinting);
		if (IsSliding == null)
		{
			throw new Exception("PlayerBodyV2.IsSliding cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		IsSliding.Initialize(this);
		__nameNetworkVariable(IsSliding, "IsSliding");
		NetworkVariableFields.Add(IsSliding);
		if (IsStopping == null)
		{
			throw new Exception("PlayerBodyV2.IsStopping cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		IsStopping.Initialize(this);
		__nameNetworkVariable(IsStopping, "IsStopping");
		NetworkVariableFields.Add(IsStopping);
		if (IsExtendedLeft == null)
		{
			throw new Exception("PlayerBodyV2.IsExtendedLeft cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		IsExtendedLeft.Initialize(this);
		__nameNetworkVariable(IsExtendedLeft, "IsExtendedLeft");
		NetworkVariableFields.Add(IsExtendedLeft);
		if (IsExtendedRight == null)
		{
			throw new Exception("PlayerBodyV2.IsExtendedRight cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		IsExtendedRight.Initialize(this);
		__nameNetworkVariable(IsExtendedRight, "IsExtendedRight");
		NetworkVariableFields.Add(IsExtendedRight);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		base.__initializeRpcs();
	}

	protected internal override string __getTypeName()
	{
		return "PlayerBodyV2";
	}
}
