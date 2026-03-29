using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerPosition : NetworkBehaviour
{
	public string Name;

	public PlayerTeam Team;

	public PlayerRole Role;

	[HideInInspector]
	public NetworkVariable<NetworkObjectReference> ClaimedByReference = new NetworkVariable<NetworkObjectReference>();

	[HideInInspector]
	public Player ClaimedBy;

	[HideInInspector]
	public bool IsClaimed => ClaimedBy != null;

	public override void OnNetworkSpawn()
	{
		ClaimedByReference.Initialize(this);
		NetworkVariable<NetworkObjectReference> claimedByReference = ClaimedByReference;
		claimedByReference.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Combine(claimedByReference.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnPlayerPositionClaimedByReferenceChanged));
		Client_InitializeNetworkVariables();
		base.OnNetworkSpawn();
	}

	protected override void OnNetworkSessionSynchronized()
	{
		Client_InitializeNetworkVariables();
		base.OnNetworkSessionSynchronized();
	}

	public override void OnNetworkDespawn()
	{
		NetworkVariable<NetworkObjectReference> claimedByReference = ClaimedByReference;
		claimedByReference.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Remove(claimedByReference.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnPlayerPositionClaimedByReferenceChanged));
		ClaimedByReference.Dispose();
		base.OnNetworkDespawn();
	}

	private void OnPlayerPositionClaimedByReferenceChanged(NetworkObjectReference oldClaimedByReferece, NetworkObjectReference newClaimedByReferece)
	{
		Player playerFromNetworkObjectReference = NetworkingUtils.GetPlayerFromNetworkObjectReference(oldClaimedByReferece);
		Player value = (ClaimedBy = NetworkingUtils.GetPlayerFromNetworkObjectReference(newClaimedByReferece));
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPlayerPositionClaimedByChanged", new Dictionary<string, object>
		{
			{ "playerPosition", this },
			{ "oldClaimedBy", playerFromNetworkObjectReference },
			{ "newClaimedBy", value }
		});
	}

	public void Server_Claim(Player player)
	{
		ClaimedByReference.Value = new NetworkObjectReference(player.NetworkObject);
	}

	public void Server_Unclaim()
	{
		ClaimedByReference.Value = default(NetworkObjectReference);
	}

	public void Client_InitializeNetworkVariables()
	{
		OnPlayerPositionClaimedByReferenceChanged(ClaimedByReference.Value, ClaimedByReference.Value);
	}

	protected override void __initializeVariables()
	{
		if (ClaimedByReference == null)
		{
			throw new Exception("PlayerPosition.ClaimedByReference cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		ClaimedByReference.Initialize(this);
		__nameNetworkVariable(ClaimedByReference, "ClaimedByReference");
		NetworkVariableFields.Add(ClaimedByReference);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		base.__initializeRpcs();
	}

	protected internal override string __getTypeName()
	{
		return "PlayerPosition";
	}
}
