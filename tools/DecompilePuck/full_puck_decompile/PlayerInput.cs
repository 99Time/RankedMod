using System.Collections.Generic;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : NetworkBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private Vector3 initialLookAngle = new Vector3(30f, 0f, 0f);

	[Space(20f)]
	[SerializeField]
	private Vector2 initialStickRaycastOriginAngle = new Vector2(40f, 80f);

	[SerializeField]
	private Vector2 minimumStickRaycastOriginAngle = new Vector2(-25f, -92.5f);

	[SerializeField]
	private Vector2 maximumStickRaycastOriginAngle = new Vector2(80f, 92.5f);

	[Space(20f)]
	[SerializeField]
	private Vector2 minimumLookAngle = new Vector2(-25f, -135f);

	[SerializeField]
	private Vector2 maximumLookAngle = new Vector2(75f, 135f);

	[Space(20f)]
	[SerializeField]
	private int minimumBladeAngle = -4;

	[SerializeField]
	private int maximumBladeAngle = 4;

	[Space(20f)]
	[SerializeField]
	private Vector2 debugShootStartAngle = new Vector2(37.5f, 90f);

	[SerializeField]
	private Vector2 debugShootEndAngle = new Vector2(37.5f, -90f);

	public NetworkedInput<Vector2> MoveInput = new NetworkedInput<Vector2>();

	public NetworkedInput<Vector2> StickRaycastOriginAngleInput = new NetworkedInput<Vector2>(default(Vector2), (Vector2 lastSentValue, Vector2 clientValue) => Vector2.Distance(lastSentValue, clientValue) > 0.1f);

	public NetworkedInput<Vector2> LookAngleInput = new NetworkedInput<Vector2>(default(Vector2), (Vector2 lastSentValue, Vector2 clientValue) => Vector2.Distance(lastSentValue, clientValue) > 0.1f);

	public NetworkedInput<sbyte> BladeAngleInput = new NetworkedInput<sbyte>(0);

	public NetworkedInput<bool> SlideInput = new NetworkedInput<bool>(initialValue: false);

	public NetworkedInput<bool> SprintInput = new NetworkedInput<bool>(initialValue: false);

	public NetworkedInput<bool> TrackInput = new NetworkedInput<bool>(initialValue: false);

	public NetworkedInput<bool> LookInput = new NetworkedInput<bool>(initialValue: false);

	public NetworkedInput<byte> JumpInput = new NetworkedInput<byte>(0, null, (byte lastReceivedValue, double lastReceivedTime, byte serverValue) => Time.timeAsDouble - lastReceivedTime > 0.5);

	public NetworkedInput<bool> StopInput = new NetworkedInput<bool>(initialValue: false);

	public NetworkedInput<byte> TwistLeftInput = new NetworkedInput<byte>(0, null, (byte lastReceivedValue, double lastReceivedTime, byte serverValue) => Time.timeAsDouble - lastReceivedTime > 0.5);

	public NetworkedInput<byte> TwistRightInput = new NetworkedInput<byte>(0, null, (byte lastReceivedValue, double lastReceivedTime, byte serverValue) => Time.timeAsDouble - lastReceivedTime > 0.5);

	public NetworkedInput<byte> DashLeftInput = new NetworkedInput<byte>(0, null, (byte lastReceivedValue, double lastReceivedTime, byte serverValue) => Time.timeAsDouble - lastReceivedTime > 0.25);

	public NetworkedInput<byte> DashRightInput = new NetworkedInput<byte>(0, null, (byte lastReceivedValue, double lastReceivedTime, byte serverValue) => Time.timeAsDouble - lastReceivedTime > 0.25);

	public NetworkedInput<bool> ExtendLeftInput = new NetworkedInput<bool>(initialValue: false);

	public NetworkedInput<bool> ExtendRightInput = new NetworkedInput<bool>(initialValue: false);

	public NetworkedInput<bool> LateralLeftInput = new NetworkedInput<bool>(initialValue: false);

	public NetworkedInput<bool> LateralRightInput = new NetworkedInput<bool>(initialValue: false);

	public NetworkedInput<bool> TalkInput = new NetworkedInput<bool>(initialValue: false);

	public NetworkedInput<bool> SleepInput = new NetworkedInput<bool>(initialValue: false);

	[HideInInspector]
	public Player Player;

	[HideInInspector]
	public int TickRate = 200;

	[HideInInspector]
	public float SleepTimeout = 60f;

	private float bladeAngleBuffer;

	private bool shouldUpdateInputs;

	private bool shouldTickInputs;

	private float tickAccumulator;

	private Tween sleepingTween;

	private bool debugInputs;

	private float lastDebugInputUpdateTime;

	private Tween debugShootTween;

	[HideInInspector]
	public Vector2 MinimumStickRaycastOriginAngle => minimumStickRaycastOriginAngle;

	[HideInInspector]
	public Vector2 MaximumStickRaycastOriginAngle => maximumStickRaycastOriginAngle;

	[HideInInspector]
	public Vector2 MinimumLookAngle => minimumLookAngle;

	[HideInInspector]
	public Vector2 MaximumLookAngle => maximumLookAngle;

	[HideInInspector]
	public int MinimumBladeAngle => minimumBladeAngle;

	[HideInInspector]
	public int MaximumBladeAngle => maximumBladeAngle;

	[HideInInspector]
	public float InitialLookAngle
	{
		get
		{
			return initialLookAngle.x;
		}
		set
		{
			initialLookAngle = new Vector3(value, 0f, 0f);
		}
	}

	private void Awake()
	{
		Player = GetComponent<Player>();
	}

	private void Start()
	{
		InputSystem.onActionChange += OnInputActionChangeCallback;
	}

	public override void OnNetworkSpawn()
	{
		if (Player.IsReplay.Value)
		{
			shouldTickInputs = true;
		}
		else if (base.IsOwner)
		{
			MonoBehaviourSingleton<InputManager>.Instance.DebugInputsAction.performed += OnDebugInputsActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.DebugShootAction.performed += OnDebugShootActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.BladeAngleUpAction.performed += OnBladeAngleUpActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.BladeAngleDownAction.performed += OnBladeAngleDownActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.JumpAction.performed += OnJumpActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.TwistLeftAction.performed += OnTwistLeftActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.TwistRightAction.performed += OnTwistRightActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.DashLeftAction.performed += OnDashLeftActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.DashRightAction.performed += OnDashRightActionPerformed;
			shouldUpdateInputs = true;
			shouldTickInputs = true;
			base.OnNetworkSpawn();
		}
	}

	public override void OnNetworkDespawn()
	{
		if (base.IsOwner)
		{
			shouldUpdateInputs = false;
			shouldTickInputs = false;
			MonoBehaviourSingleton<InputManager>.Instance.DebugInputsAction.performed -= OnDebugInputsActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.DebugShootAction.performed -= OnDebugShootActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.BladeAngleUpAction.performed -= OnBladeAngleUpActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.BladeAngleDownAction.performed -= OnBladeAngleDownActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.JumpAction.performed -= OnJumpActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.TwistLeftAction.performed -= OnTwistLeftActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.TwistRightAction.performed -= OnTwistRightActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.DashLeftAction.performed -= OnDashLeftActionPerformed;
			MonoBehaviourSingleton<InputManager>.Instance.DashRightAction.performed -= OnDashRightActionPerformed;
			base.OnNetworkDespawn();
		}
	}

	public override void OnDestroy()
	{
		InputSystem.onActionChange -= OnInputActionChangeCallback;
		sleepingTween?.Kill();
		base.OnDestroy();
	}

	private void Update()
	{
		if (shouldUpdateInputs)
		{
			UpdateInputs();
		}
		if (!shouldTickInputs)
		{
			return;
		}
		tickAccumulator += Time.deltaTime * (float)TickRate;
		if (tickAccumulator >= 1f)
		{
			while (tickAccumulator >= 1f)
			{
				tickAccumulator -= 1f;
			}
			ClientTick();
		}
	}

	private void UpdateInputs()
	{
		if (NetworkBehaviourSingleton<UIManager>.Instance.isMouseActive)
		{
			return;
		}
		if (debugInputs)
		{
			if (Time.time - lastDebugInputUpdateTime > 1f)
			{
				MoveInput.ClientValue = new Vector2((Random.Range(0f, 1f) > 0.5f) ? 1 : (-1), (Random.Range(0f, 1f) > 0.5f) ? 1 : (-1));
				SlideInput.ClientValue = Random.Range(0f, 1f) > 0.5f;
				SprintInput.ClientValue = Random.Range(0f, 1f) > 0.5f;
				TrackInput.ClientValue = Random.Range(0f, 1f) > 0.5f;
				if (!TrackInput.ClientValue)
				{
					LookInput.ClientValue = Random.Range(0f, 1f) > 0.5f;
				}
				ExtendLeftInput.ClientValue = Random.Range(0f, 1f) > 0.5f;
				ExtendRightInput.ClientValue = Random.Range(0f, 1f) > 0.5f;
				LateralLeftInput.ClientValue = Random.Range(0f, 1f) > 0.5f;
				LateralRightInput.ClientValue = Random.Range(0f, 1f) > 0.5f;
				StopInput.ClientValue = Random.Range(0f, 1f) > 0.5f;
				if (LookInput.ClientValue)
				{
					LookAngleInput.ClientValue = new Vector2(Random.Range(minimumLookAngle.x, maximumLookAngle.x), Random.Range(minimumLookAngle.y, maximumLookAngle.y));
				}
				else
				{
					StickRaycastOriginAngleInput.ClientValue = new Vector2(Random.Range(minimumStickRaycastOriginAngle.x, maximumStickRaycastOriginAngle.x), Random.Range(minimumStickRaycastOriginAngle.y, maximumStickRaycastOriginAngle.y));
				}
				lastDebugInputUpdateTime = Time.time;
			}
			return;
		}
		MoveInput.ClientValue = new Vector2((MonoBehaviourSingleton<InputManager>.Instance.TurnRightAction.IsInProgress() ? 1 : 0) + (MonoBehaviourSingleton<InputManager>.Instance.TurnLeftAction.IsInProgress() ? (-1) : 0), (MonoBehaviourSingleton<InputManager>.Instance.MoveForwardAction.IsInProgress() ? 1 : 0) + (MonoBehaviourSingleton<InputManager>.Instance.MoveBackwardAction.IsInProgress() ? (-1) : 0));
		if (!LookInput.ClientValue)
		{
			Vector2 vector = MonoBehaviourSingleton<InputManager>.Instance.StickAction.ReadValue<Vector2>();
			Vector2 vector2 = new Vector2((0f - vector.y) * (MonoBehaviourSingleton<SettingsManager>.Instance.GlobalStickSensitivity / 2f) * MonoBehaviourSingleton<SettingsManager>.Instance.VerticalStickSensitivity, vector.x * (MonoBehaviourSingleton<SettingsManager>.Instance.GlobalStickSensitivity / 2f) * MonoBehaviourSingleton<SettingsManager>.Instance.HorizontalStickSensitivity);
			if (debugShootTween == null || !debugShootTween.active)
			{
				StickRaycastOriginAngleInput.ClientValue = Utils.Vector2Clamp(StickRaycastOriginAngleInput.ClientValue + vector2, minimumStickRaycastOriginAngle, maximumStickRaycastOriginAngle);
			}
		}
		SlideInput.ClientValue = MonoBehaviourSingleton<InputManager>.Instance.SlideAction.IsInProgress();
		SprintInput.ClientValue = MonoBehaviourSingleton<InputManager>.Instance.SprintAction.IsInProgress();
		TrackInput.ClientValue = MonoBehaviourSingleton<InputManager>.Instance.TrackAction.IsInProgress();
		LookInput.ClientValue = MonoBehaviourSingleton<InputManager>.Instance.LookAction.IsInProgress();
		ExtendLeftInput.ClientValue = MonoBehaviourSingleton<InputManager>.Instance.ExtendLeftAction.IsInProgress();
		ExtendRightInput.ClientValue = MonoBehaviourSingleton<InputManager>.Instance.ExtendRightAction.IsInProgress();
		TalkInput.ClientValue = MonoBehaviourSingleton<InputManager>.Instance.TalkAction.IsInProgress();
		StopInput.ClientValue = MonoBehaviourSingleton<InputManager>.Instance.StopAction.IsInProgress();
	}

	public void UpdateLookAngle(float deltaTime)
	{
		if (TrackInput.ClientValue && !LookInput.ClientValue)
		{
			Puck puck = NetworkBehaviourSingleton<PuckManager>.Instance.GetPlayerPuck(base.OwnerClientId);
			if (!puck)
			{
				puck = NetworkBehaviourSingleton<PuckManager>.Instance.GetPuck();
			}
			PlayerCamera playerCamera = Player.PlayerCamera;
			PlayerBodyV2 playerBody = Player.PlayerBody;
			if ((bool)puck && (bool)playerCamera && (bool)playerBody)
			{
				Quaternion quaternion = Quaternion.LookRotation(puck.transform.position - playerCamera.transform.position);
				Vector3 vector = Utils.WrapEulerAngles((Quaternion.Inverse(playerBody.transform.rotation) * quaternion).eulerAngles);
				vector = Utils.Vector2Clamp(vector, minimumLookAngle, maximumLookAngle);
				LookAngleInput.ClientValue = Vector3.LerpUnclamped(LookAngleInput.ClientValue, vector, deltaTime * 10f);
			}
		}
		if (LookInput.ClientValue)
		{
			Vector2 vector2 = MonoBehaviourSingleton<InputManager>.Instance.StickAction.ReadValue<Vector2>();
			Vector2 vector3 = new Vector2((0f - vector2.y) * (MonoBehaviourSingleton<SettingsManager>.Instance.LookSensitivity / 2f), vector2.x * (MonoBehaviourSingleton<SettingsManager>.Instance.LookSensitivity / 2f));
			LookAngleInput.ClientValue = Utils.Vector2Clamp(LookAngleInput.ClientValue + vector3, minimumLookAngle, maximumLookAngle);
		}
		else if (!TrackInput.ClientValue)
		{
			LookAngleInput.ClientValue = Vector3.Lerp(LookAngleInput.ClientValue, initialLookAngle, deltaTime * 10f);
		}
	}

	public void ResetInputs(bool invertStickRaycastOriginAngle = false)
	{
		MoveInput.ClientValue = Vector2.zero;
		LookAngleInput.ClientValue = initialLookAngle;
		StickRaycastOriginAngleInput.ClientValue = initialStickRaycastOriginAngle;
		if (invertStickRaycastOriginAngle)
		{
			StickRaycastOriginAngleInput.ClientValue.y = 0f - StickRaycastOriginAngleInput.ClientValue.y;
		}
		BladeAngleInput.ClientValue = 0;
		SlideInput.ClientValue = false;
		SprintInput.ClientValue = false;
		TrackInput.ClientValue = false;
		LookInput.ClientValue = false;
		ExtendLeftInput.ClientValue = false;
		ExtendRightInput.ClientValue = false;
		LateralLeftInput.ClientValue = false;
		LateralRightInput.ClientValue = false;
		StopInput.ClientValue = false;
		bladeAngleBuffer = BladeAngleInput.ClientValue;
		MonoBehaviourSingleton<InputManager>.Instance.Reset();
	}

	private void OnDebugShootActionPerformed(InputAction.CallbackContext context)
	{
		if (!NetworkBehaviourSingleton<UIManager>.Instance.isMouseActive && (bool)Player)
		{
			StickRaycastOriginAngleInput.ClientValue = debugShootStartAngle;
			debugShootTween?.Kill();
			debugShootTween = DOTween.Sequence().AppendInterval(0.2f).Append(DOTween.To(() => StickRaycastOriginAngleInput.ClientValue, delegate(Vector2 x)
			{
				StickRaycastOriginAngleInput.ClientValue = x;
			}, debugShootEndAngle, 0.2f).SetEase(Ease.Linear))
				.Append(DOTween.To(() => StickRaycastOriginAngleInput.ClientValue, delegate(Vector2 x)
				{
					StickRaycastOriginAngleInput.ClientValue = x;
				}, debugShootStartAngle, 0.2f).SetEase(Ease.Linear));
		}
	}

	private void OnDebugInputsActionPerformed(InputAction.CallbackContext context)
	{
		if (!NetworkBehaviourSingleton<UIManager>.Instance.isMouseActive && (bool)Player)
		{
			debugInputs = !debugInputs;
		}
	}

	private void OnBladeAngleUpActionPerformed(InputAction.CallbackContext context)
	{
		if (!NetworkBehaviourSingleton<UIManager>.Instance.isMouseActive && (bool)Player.Stick)
		{
			bladeAngleBuffer += context.ReadValue<float>();
			bladeAngleBuffer = Mathf.Clamp(bladeAngleBuffer, minimumBladeAngle, maximumBladeAngle);
			BladeAngleInput.ClientValue = (sbyte)bladeAngleBuffer;
		}
	}

	private void OnBladeAngleDownActionPerformed(InputAction.CallbackContext context)
	{
		if (!NetworkBehaviourSingleton<UIManager>.Instance.isMouseActive && (bool)Player.Stick)
		{
			bladeAngleBuffer -= context.ReadValue<float>();
			bladeAngleBuffer = Mathf.Clamp(bladeAngleBuffer, minimumBladeAngle, maximumBladeAngle);
			BladeAngleInput.ClientValue = (sbyte)bladeAngleBuffer;
		}
	}

	private void OnJumpActionPerformed(InputAction.CallbackContext context)
	{
		if (!NetworkBehaviourSingleton<UIManager>.Instance.isMouseActive)
		{
			JumpInput.ClientValue++;
		}
	}

	private void OnTwistLeftActionPerformed(InputAction.CallbackContext context)
	{
		if (!NetworkBehaviourSingleton<UIManager>.Instance.isMouseActive)
		{
			TwistLeftInput.ClientValue++;
		}
	}

	private void OnTwistRightActionPerformed(InputAction.CallbackContext context)
	{
		if (!NetworkBehaviourSingleton<UIManager>.Instance.isMouseActive)
		{
			TwistRightInput.ClientValue++;
		}
	}

	private void OnDashLeftActionPerformed(InputAction.CallbackContext context)
	{
		if (!NetworkBehaviourSingleton<UIManager>.Instance.isMouseActive)
		{
			DashLeftInput.ClientValue++;
		}
	}

	private void OnDashRightActionPerformed(InputAction.CallbackContext context)
	{
		if (!NetworkBehaviourSingleton<UIManager>.Instance.isMouseActive)
		{
			DashRightInput.ClientValue++;
		}
	}

	private void ClientTick()
	{
		if (MoveInput.HasChanged)
		{
			short x = (short)(MoveInput.ClientValue.x * 32767f);
			short y = (short)(MoveInput.ClientValue.y * 32767f);
			Client_MoveInputRpc(x, y);
			MoveInput.ClientTick();
		}
		if (StickRaycastOriginAngleInput.HasChanged)
		{
			short x2 = (short)(StickRaycastOriginAngleInput.ClientValue.x / 360f * 32767f);
			short y2 = (short)(StickRaycastOriginAngleInput.ClientValue.y / 360f * 32767f);
			Client_RaycastOriginAngleInputRpc(x2, y2);
			StickRaycastOriginAngleInput.ClientTick();
		}
		if (LookAngleInput.HasChanged)
		{
			short x3 = (short)(LookAngleInput.ClientValue.x / 360f * 32767f);
			short y3 = (short)(LookAngleInput.ClientValue.y / 360f * 32767f);
			Client_LookAngleInputRpc(x3, y3);
			LookAngleInput.ClientTick();
		}
		if (BladeAngleInput.HasChanged)
		{
			Client_BladeAngleInputRpc(BladeAngleInput.ClientValue);
			BladeAngleInput.ClientTick();
		}
		if (SlideInput.HasChanged)
		{
			Client_SlideInputRpc(SlideInput.ClientValue);
			SlideInput.ClientTick();
		}
		if (SprintInput.HasChanged)
		{
			Client_SprintInputRpc(SprintInput.ClientValue);
			SprintInput.ClientTick();
		}
		if (TrackInput.HasChanged)
		{
			Client_TrackInputRpc(TrackInput.ClientValue);
			TrackInput.ClientTick();
		}
		if (LookInput.HasChanged)
		{
			Client_LookInputRpc(LookInput.ClientValue);
			LookInput.ClientTick();
		}
		if (JumpInput.HasChanged)
		{
			Client_JumpInputRpc();
			JumpInput.ClientTick();
		}
		if (StopInput.HasChanged)
		{
			Client_StopInputRpc(StopInput.ClientValue);
			StopInput.ClientTick();
		}
		if (TwistLeftInput.HasChanged)
		{
			Client_TwistLeftInputRpc();
			TwistLeftInput.ClientTick();
		}
		if (TwistRightInput.HasChanged)
		{
			Client_TwistRightInputRpc();
			TwistRightInput.ClientTick();
		}
		if (DashLeftInput.HasChanged)
		{
			Client_DashLeftInputRpc();
			DashLeftInput.ClientTick();
		}
		if (DashRightInput.HasChanged)
		{
			Client_DashRightInputRpc();
			DashRightInput.ClientTick();
		}
		if (ExtendLeftInput.HasChanged)
		{
			Client_ExtendLeftInputRpc(ExtendLeftInput.ClientValue);
			ExtendLeftInput.ClientTick();
		}
		if (ExtendRightInput.HasChanged)
		{
			Client_ExtendRightInputRpc(ExtendRightInput.ClientValue);
			ExtendRightInput.ClientTick();
		}
		if (LateralLeftInput.HasChanged)
		{
			Client_LateralLeftInputRpc(LateralLeftInput.ClientValue);
			LateralLeftInput.ClientTick();
		}
		if (LateralRightInput.HasChanged)
		{
			Client_LateralRightInputRpc(LateralRightInput.ClientValue);
			LateralRightInput.ClientTick();
		}
		if (TalkInput.HasChanged)
		{
			Client_TalkInputRpc(TalkInput.ClientValue);
			TalkInput.ClientTick();
		}
		if (SleepInput.HasChanged)
		{
			Client_SleepInputRpc(SleepInput.ClientValue);
			SleepInput.ClientTick();
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_MoveInputRpc(short x, short y)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(354985997u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, x);
				BytePacker.WriteValueBitPacked(bufferWriter, y);
				__endSendRpc(ref bufferWriter, 354985997u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Vector2 value = new Vector2((float)x / 32767f, (float)y / 32767f);
				MoveInput.ServerValue = Utils.Vector2Clamp(value, -Vector2.one, Vector2.one);
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
	public void Server_MoveInputRpc(Vector2 value, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					Delivery = RpcDelivery.Unreliable
				};
				FastBufferWriter bufferWriter = __beginSendRpc(2333051307u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Unreliable);
				bufferWriter.WriteValueSafe(in value);
				__endSendRpc(ref bufferWriter, 2333051307u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Unreliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				MoveInput.ServerValue = value;
			}
		}
	}

	[Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
	public void Client_RaycastOriginAngleInputRpc(short x, short y)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				Delivery = RpcDelivery.Unreliable
			};
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(3072819325u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
			BytePacker.WriteValueBitPacked(bufferWriter, x);
			BytePacker.WriteValueBitPacked(bufferWriter, y);
			__endSendRpc(ref bufferWriter, 3072819325u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			Vector2 value = new Vector2((float)(x * 360) / 32767f, (float)(y * 360) / 32767f);
			if ((bool)Player)
			{
				StickRaycastOriginAngleInput.ServerValue = Utils.Vector2Clamp(value, minimumStickRaycastOriginAngle, maximumStickRaycastOriginAngle);
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
	public void Server_RaycastOriginAngleInputRpc(Vector2 value, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					Delivery = RpcDelivery.Unreliable
				};
				FastBufferWriter bufferWriter = __beginSendRpc(3003669798u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Unreliable);
				bufferWriter.WriteValueSafe(in value);
				__endSendRpc(ref bufferWriter, 3003669798u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Unreliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				StickRaycastOriginAngleInput.ServerValue = value;
			}
		}
	}

	[Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
	public void Client_LookAngleInputRpc(short x, short y)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				Delivery = RpcDelivery.Unreliable
			};
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(3839358977u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
			BytePacker.WriteValueBitPacked(bufferWriter, x);
			BytePacker.WriteValueBitPacked(bufferWriter, y);
			__endSendRpc(ref bufferWriter, 3839358977u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			Vector2 value = new Vector2((float)(x * 360) / 32767f, (float)(y * 360) / 32767f);
			if ((bool)Player)
			{
				LookAngleInput.ServerValue = Utils.Vector2Clamp(value, minimumLookAngle, maximumLookAngle);
				Server_LookAngleInputRpc(x, y, base.RpcTarget.NotServer);
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
	public void Server_LookAngleInputRpc(short x, short y, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					Delivery = RpcDelivery.Unreliable
				};
				FastBufferWriter bufferWriter = __beginSendRpc(1047632353u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Unreliable);
				BytePacker.WriteValueBitPacked(bufferWriter, x);
				BytePacker.WriteValueBitPacked(bufferWriter, y);
				__endSendRpc(ref bufferWriter, 1047632353u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Unreliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Vector2 serverValue = new Vector2((float)(x * 360) / 32767f, (float)(y * 360) / 32767f);
				LookAngleInput.ServerValue = serverValue;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_BladeAngleInputRpc(sbyte value)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(2671629003u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 2671629003u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if ((bool)Player && (bool)Player.Stick)
			{
				BladeAngleInput.ServerValue = (sbyte)Mathf.Clamp(value, minimumBladeAngle, maximumBladeAngle);
				Server_BladeAngleInputRpc(BladeAngleInput.ServerValue, base.RpcTarget.NotServer);
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams)]
	public void Server_BladeAngleInputRpc(sbyte value, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				FastBufferWriter bufferWriter = __beginSendRpc(817646686u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 817646686u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BladeAngleInput.ServerValue = value;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_SlideInputRpc(bool value)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(804686296u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 804686296u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SlideInput.ServerValue = value;
				Server_SlideInputRpc(SlideInput.ServerValue, base.RpcTarget.NotServer);
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams)]
	public void Server_SlideInputRpc(bool value, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				FastBufferWriter bufferWriter = __beginSendRpc(4107840079u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 4107840079u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SlideInput.ServerValue = value;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_SprintInputRpc(bool value)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(2917244568u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2917244568u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SprintInput.ServerValue = value;
				Server_SprintInputRpc(SprintInput.ServerValue, base.RpcTarget.NotServer);
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams)]
	public void Server_SprintInputRpc(bool value, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				FastBufferWriter bufferWriter = __beginSendRpc(778340344u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 778340344u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SprintInput.ServerValue = value;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_TrackInputRpc(bool value)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(3765825011u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3765825011u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				TrackInput.ServerValue = value;
				Server_TrackInputRpc(TrackInput.ServerValue, base.RpcTarget.NotServer);
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams)]
	public void Server_TrackInputRpc(bool value, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				FastBufferWriter bufferWriter = __beginSendRpc(2722698928u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2722698928u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				TrackInput.ServerValue = value;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_LookInputRpc(bool value)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(3995092734u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3995092734u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				LookInput.ServerValue = value;
				Server_LookInputRpc(LookInput.ServerValue, base.RpcTarget.NotServer);
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams)]
	public void Server_LookInputRpc(bool value, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				FastBufferWriter bufferWriter = __beginSendRpc(3779091983u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3779091983u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				LookInput.ServerValue = value;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_JumpInputRpc()
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(4077849638u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 4077849638u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (JumpInput.ShouldChange)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnPlayerJumpInput", new Dictionary<string, object> { { "player", Player } });
				JumpInput.ServerTick();
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_StopInputRpc(bool value)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(3261073083u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3261073083u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				StopInput.ServerValue = value;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_DashLeftInputRpc()
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(3013974635u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 3013974635u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (DashLeftInput.ShouldChange)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnPlayerDashLeftInput", new Dictionary<string, object> { { "player", Player } });
				DashLeftInput.ServerTick();
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_DashRightInputRpc()
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(341272022u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 341272022u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (DashRightInput.ShouldChange)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnPlayerDashRightInput", new Dictionary<string, object> { { "player", Player } });
				DashRightInput.ServerTick();
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_TwistLeftInputRpc()
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(1583302350u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 1583302350u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (TwistLeftInput.ShouldChange)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnPlayerTwistLeftInput", new Dictionary<string, object> { { "player", Player } });
				TwistLeftInput.ServerTick();
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_TwistRightInputRpc()
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			RpcParams rpcParams = default(RpcParams);
			FastBufferWriter bufferWriter = __beginSendRpc(2380271940u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 2380271940u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (TwistRightInput.ShouldChange)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnPlayerTwistRightInput", new Dictionary<string, object> { { "player", Player } });
				TwistRightInput.ServerTick();
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_ExtendLeftInputRpc(bool value)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(2897220457u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2897220457u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ExtendLeftInput.ServerValue = value;
				Server_ExtendLeftInputRpc(ExtendLeftInput.ServerValue, base.RpcTarget.NotServer);
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams)]
	public void Server_ExtendLeftInputRpc(bool value, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				FastBufferWriter bufferWriter = __beginSendRpc(3288109408u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3288109408u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ExtendLeftInput.ServerValue = value;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_ExtendRightInputRpc(bool value)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(2892078582u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2892078582u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ExtendRightInput.ServerValue = value;
				Server_ExtendRightInputRpc(ExtendRightInput.ServerValue, base.RpcTarget.NotServer);
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams)]
	public void Server_ExtendRightInputRpc(bool value, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				FastBufferWriter bufferWriter = __beginSendRpc(152722375u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 152722375u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ExtendRightInput.ServerValue = value;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_LateralLeftInputRpc(bool value)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(3433706111u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3433706111u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				LateralLeftInput.ServerValue = value;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_LateralRightInputRpc(bool value)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(1602051713u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1602051713u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				LateralRightInput.ServerValue = value;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_TalkInputRpc(bool value)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(1234853793u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1234853793u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				TalkInput.ServerValue = value;
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerTalkInput", new Dictionary<string, object>
				{
					{ "player", Player },
					{ "value", value }
				});
				Server_TalkInputRpc(TalkInput.ServerValue, base.RpcTarget.NotServer);
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams)]
	public void Server_TalkInputRpc(bool value, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				FastBufferWriter bufferWriter = __beginSendRpc(3713736028u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3713736028u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				TalkInput.ServerValue = value;
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerTalkInput", new Dictionary<string, object>
				{
					{ "player", Player },
					{ "value", value }
				});
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_SleepInputRpc(bool value)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				RpcParams rpcParams = default(RpcParams);
				FastBufferWriter bufferWriter = __beginSendRpc(1182029238u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1182029238u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SleepInput.ServerValue = value;
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnPlayerSleepInput", new Dictionary<string, object>
				{
					{ "player", Player },
					{ "value", value }
				});
			}
		}
	}

	public void Server_ForceSynchronizeClientId(ulong clientId)
	{
		if (NetworkManager.Singleton.IsServer && clientId != 0L)
		{
			Server_BladeAngleInputRpc(BladeAngleInput.ServerValue, base.RpcTarget.Single(clientId, RpcTargetUse.Persistent));
			Server_SlideInputRpc(SlideInput.ServerValue, base.RpcTarget.Single(clientId, RpcTargetUse.Persistent));
			Server_SprintInputRpc(SprintInput.ServerValue, base.RpcTarget.Single(clientId, RpcTargetUse.Persistent));
			Server_TrackInputRpc(TrackInput.ServerValue, base.RpcTarget.Single(clientId, RpcTargetUse.Persistent));
			Server_LookInputRpc(LookInput.ServerValue, base.RpcTarget.Single(clientId, RpcTargetUse.Persistent));
			Server_ExtendLeftInputRpc(ExtendLeftInput.ServerValue, base.RpcTarget.Single(clientId, RpcTargetUse.Persistent));
			Server_ExtendRightInputRpc(ExtendRightInput.ServerValue, base.RpcTarget.Single(clientId, RpcTargetUse.Persistent));
			Server_TalkInputRpc(TalkInput.ServerValue, base.RpcTarget.Single(clientId, RpcTargetUse.Persistent));
		}
	}

	private void OnInputActionChangeCallback(object obj, InputActionChange change)
	{
		if ((bool)NetworkManager.Singleton && !NetworkManager.Singleton.IsServer)
		{
			SleepInput.ClientValue = false;
			sleepingTween?.Kill();
			sleepingTween = DOVirtual.DelayedCall(SleepTimeout, delegate
			{
				SleepInput.ClientValue = true;
			});
		}
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(354985997u, __rpc_handler_354985997, "Client_MoveInputRpc");
		__registerRpc(2333051307u, __rpc_handler_2333051307, "Server_MoveInputRpc");
		__registerRpc(3072819325u, __rpc_handler_3072819325, "Client_RaycastOriginAngleInputRpc");
		__registerRpc(3003669798u, __rpc_handler_3003669798, "Server_RaycastOriginAngleInputRpc");
		__registerRpc(3839358977u, __rpc_handler_3839358977, "Client_LookAngleInputRpc");
		__registerRpc(1047632353u, __rpc_handler_1047632353, "Server_LookAngleInputRpc");
		__registerRpc(2671629003u, __rpc_handler_2671629003, "Client_BladeAngleInputRpc");
		__registerRpc(817646686u, __rpc_handler_817646686, "Server_BladeAngleInputRpc");
		__registerRpc(804686296u, __rpc_handler_804686296, "Client_SlideInputRpc");
		__registerRpc(4107840079u, __rpc_handler_4107840079, "Server_SlideInputRpc");
		__registerRpc(2917244568u, __rpc_handler_2917244568, "Client_SprintInputRpc");
		__registerRpc(778340344u, __rpc_handler_778340344, "Server_SprintInputRpc");
		__registerRpc(3765825011u, __rpc_handler_3765825011, "Client_TrackInputRpc");
		__registerRpc(2722698928u, __rpc_handler_2722698928, "Server_TrackInputRpc");
		__registerRpc(3995092734u, __rpc_handler_3995092734, "Client_LookInputRpc");
		__registerRpc(3779091983u, __rpc_handler_3779091983, "Server_LookInputRpc");
		__registerRpc(4077849638u, __rpc_handler_4077849638, "Client_JumpInputRpc");
		__registerRpc(3261073083u, __rpc_handler_3261073083, "Client_StopInputRpc");
		__registerRpc(3013974635u, __rpc_handler_3013974635, "Client_DashLeftInputRpc");
		__registerRpc(341272022u, __rpc_handler_341272022, "Client_DashRightInputRpc");
		__registerRpc(1583302350u, __rpc_handler_1583302350, "Client_TwistLeftInputRpc");
		__registerRpc(2380271940u, __rpc_handler_2380271940, "Client_TwistRightInputRpc");
		__registerRpc(2897220457u, __rpc_handler_2897220457, "Client_ExtendLeftInputRpc");
		__registerRpc(3288109408u, __rpc_handler_3288109408, "Server_ExtendLeftInputRpc");
		__registerRpc(2892078582u, __rpc_handler_2892078582, "Client_ExtendRightInputRpc");
		__registerRpc(152722375u, __rpc_handler_152722375, "Server_ExtendRightInputRpc");
		__registerRpc(3433706111u, __rpc_handler_3433706111, "Client_LateralLeftInputRpc");
		__registerRpc(1602051713u, __rpc_handler_1602051713, "Client_LateralRightInputRpc");
		__registerRpc(1234853793u, __rpc_handler_1234853793, "Client_TalkInputRpc");
		__registerRpc(3713736028u, __rpc_handler_3713736028, "Server_TalkInputRpc");
		__registerRpc(1182029238u, __rpc_handler_1182029238, "Client_SleepInputRpc");
		base.__initializeRpcs();
	}

	private static void __rpc_handler_354985997(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out short value);
			ByteUnpacker.ReadValueBitPacked(reader, out short value2);
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_MoveInputRpc(value, value2);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2333051307(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out Vector2 value);
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Server_MoveInputRpc(value, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3072819325(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out short value);
			ByteUnpacker.ReadValueBitPacked(reader, out short value2);
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_RaycastOriginAngleInputRpc(value, value2);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3003669798(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out Vector2 value);
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Server_RaycastOriginAngleInputRpc(value, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3839358977(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out short value);
			ByteUnpacker.ReadValueBitPacked(reader, out short value2);
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_LookAngleInputRpc(value, value2);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1047632353(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out short value);
			ByteUnpacker.ReadValueBitPacked(reader, out short value2);
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Server_LookAngleInputRpc(value, value2, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2671629003(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out sbyte value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_BladeAngleInputRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_817646686(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out sbyte value, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Server_BladeAngleInputRpc(value, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_804686296(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_SlideInputRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_4107840079(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Server_SlideInputRpc(value, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2917244568(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_SprintInputRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_778340344(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Server_SprintInputRpc(value, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3765825011(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_TrackInputRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2722698928(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Server_TrackInputRpc(value, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3995092734(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_LookInputRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3779091983(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Server_LookInputRpc(value, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_4077849638(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_JumpInputRpc();
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3261073083(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_StopInputRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3013974635(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_DashLeftInputRpc();
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_341272022(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_DashRightInputRpc();
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1583302350(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_TwistLeftInputRpc();
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2380271940(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_TwistRightInputRpc();
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2897220457(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_ExtendLeftInputRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3288109408(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Server_ExtendLeftInputRpc(value, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2892078582(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_ExtendRightInputRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_152722375(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Server_ExtendRightInputRpc(value, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3433706111(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_LateralLeftInputRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1602051713(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_LateralRightInputRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1234853793(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_TalkInputRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3713736028(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Server_TalkInputRpc(value, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1182029238(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerInput)target).Client_SleepInputRpc(value);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected internal override string __getTypeName()
	{
		return "PlayerInput";
	}
}
