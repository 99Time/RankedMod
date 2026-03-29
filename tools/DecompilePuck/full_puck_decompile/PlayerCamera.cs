using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerCamera : BaseCamera
{
	[HideInInspector]
	public NetworkVariable<NetworkObjectReference> PlayerReference = new NetworkVariable<NetworkObjectReference>();

	[HideInInspector]
	public Player Player;

	[HideInInspector]
	public PlayerBodyV2 PlayerBody => Player.PlayerBody;

	protected override void OnNetworkPostSpawn()
	{
		if (PlayerReference.Value.TryGet(out var networkObject))
		{
			Player = networkObject.GetComponent<Player>();
		}
		if ((bool)Player)
		{
			Player.PlayerCamera = this;
			if (Player.IsLocalPlayer)
			{
				Enable();
			}
		}
		base.OnNetworkPostSpawn();
	}

	public override void OnNetworkDespawn()
	{
		if (base.IsEnabled)
		{
			Disable();
		}
		base.OnNetworkDespawn();
	}

	public override void OnDestroy()
	{
		if (base.IsEnabled)
		{
			Disable();
		}
		base.OnDestroy();
	}

	public override void Enable()
	{
		if (!base.IsEnabled)
		{
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerCameraEnabled", new Dictionary<string, object> { { "playerCamera", this } });
		}
		base.Enable();
	}

	public override void Disable()
	{
		if (base.IsEnabled)
		{
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerCameraDisabled", new Dictionary<string, object> { { "playerCamera", this } });
		}
		base.Disable();
	}

	public override void OnTick(float deltaTime)
	{
		base.OnTick(deltaTime);
		if ((bool)Player)
		{
			PlayerInput playerInput = Player.PlayerInput;
			if ((bool)playerInput)
			{
				playerInput.UpdateLookAngle(deltaTime);
				base.transform.localRotation = Quaternion.Euler(Player.IsLocalPlayer ? playerInput.LookAngleInput.ClientValue : playerInput.LookAngleInput.ServerValue);
			}
		}
	}

	protected override void __initializeVariables()
	{
		if (PlayerReference == null)
		{
			throw new Exception("PlayerCamera.PlayerReference cannot be null. All NetworkVariableBase instances must be initialized.");
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
		return "PlayerCamera";
	}
}
