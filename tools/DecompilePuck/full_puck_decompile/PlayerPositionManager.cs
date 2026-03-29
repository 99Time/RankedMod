using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class PlayerPositionManager : NetworkBehaviourSingleton<PlayerPositionManager>
{
	private List<PlayerPosition> bluePositions = new List<PlayerPosition>();

	private List<PlayerPosition> redPositions = new List<PlayerPosition>();

	[HideInInspector]
	public List<PlayerPosition> BluePositions => bluePositions;

	[HideInInspector]
	public List<PlayerPosition> RedPositions => redPositions;

	[HideInInspector]
	public List<PlayerPosition> AllPositions => bluePositions.Concat(redPositions).ToList();

	public void SetBluePositions(List<PlayerPosition> bluePositions)
	{
		this.bluePositions = bluePositions;
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnBluePlayerPositionsSet");
	}

	public void SetRedPositions(List<PlayerPosition> redPositions)
	{
		this.redPositions = redPositions;
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnRedPlayerPositionsSet");
	}

	public void Server_UnclaimAllPositions()
	{
		foreach (PlayerPosition allPosition in AllPositions)
		{
			allPosition.Server_Unclaim();
		}
	}

	[Rpc(SendTo.Server)]
	public void Client_ClaimPositionRpc(NetworkObjectReference playerPositionNetworkObjectReference, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcParams rpcParams2 = rpcParams;
			RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
			FastBufferWriter bufferWriter = __beginSendRpc(4027053218u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in playerPositionNetworkObjectReference, default(FastBufferWriter.ForNetworkSerializable));
			__endSendRpc(ref bufferWriter, 4027053218u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		Player playerByClientId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(rpcParams.Receive.SenderClientId);
		PlayerPosition playerPosition = playerByClientId.PlayerPosition;
		if (!playerPositionNetworkObjectReference.TryGet(out var networkObject))
		{
			return;
		}
		PlayerPosition component = networkObject.GetComponent<PlayerPosition>();
		if ((bool)playerByClientId && (bool)component && !component.IsClaimed && (bool)playerByClientId && playerByClientId.Team.Value == component.Team)
		{
			if ((bool)playerPosition)
			{
				playerPosition.Server_Unclaim();
			}
			component.Server_Claim(playerByClientId);
		}
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(4027053218u, __rpc_handler_4027053218, "Client_ClaimPositionRpc");
		base.__initializeRpcs();
	}

	private static void __rpc_handler_4027053218(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((PlayerPositionManager)target).Client_ClaimPositionRpc(value, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected internal override string __getTypeName()
	{
		return "PlayerPositionManager";
	}
}
