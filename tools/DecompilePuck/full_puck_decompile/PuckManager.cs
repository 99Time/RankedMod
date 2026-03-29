using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class PuckManager : NetworkBehaviourSingleton<PuckManager>
{
	[Header("Prefabs")]
	[SerializeField]
	private Puck puckPrefab;

	private List<PuckPosition> puckPositions = new List<PuckPosition>();

	private List<Puck> pucks = new List<Puck>();

	public void SetPuckPositions(List<PuckPosition> puckPositions)
	{
		this.puckPositions = puckPositions;
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPuckPositionsSet");
	}

	public void ClearPuckPositions()
	{
		puckPositions.Clear();
	}

	public void AddPuck(Puck puck)
	{
		pucks.Add(puck);
	}

	public void RemovePuck(Puck puck)
	{
		pucks.Remove(puck);
	}

	public List<Puck> GetPucks(bool includeReplay = false)
	{
		if (includeReplay)
		{
			return pucks;
		}
		return pucks.Where((Puck puck) => !puck.IsReplay.Value).ToList();
	}

	public List<Puck> GetReplayPucks()
	{
		return pucks.Where((Puck puck) => puck.IsReplay.Value).ToList();
	}

	public Puck GetPuck(bool includeReplay = false)
	{
		return GetPucks(includeReplay).FirstOrDefault((Puck puck) => puck);
	}

	public Puck GetPlayerPuck(ulong clientId)
	{
		Player playerByClientId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId);
		if (!playerByClientId)
		{
			return null;
		}
		if (!playerByClientId.Stick)
		{
			return null;
		}
		if (playerByClientId.Stick.NetworkObjectCollisionBuffer.Buffer.LastOrDefault().NetworkObjectReference.TryGet(out var networkObject))
		{
			return networkObject.GetComponent<Puck>();
		}
		return null;
	}

	public Puck GetPuckByNetworkObjectId(ulong networkObjectId)
	{
		return GetPucks().FirstOrDefault((Puck puck) => puck.NetworkObjectId == networkObjectId);
	}

	public Puck GetReplayPuckByNetworkObjectId(ulong networkObjectId)
	{
		return GetReplayPucks().FirstOrDefault((Puck puck) => puck.NetworkObjectId == networkObjectId);
	}

	public Puck Server_SpawnPuck(Vector3 position, Quaternion rotation, Vector3 velocity, bool isReplay = false)
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return null;
		}
		Puck puck = Object.Instantiate(puckPrefab, position, rotation);
		puck.IsReplay.Value = isReplay;
		puck.Rigidbody.AddForce(velocity, ForceMode.VelocityChange);
		puck.NetworkObject.Spawn();
		Debug.Log($"[PuckManager] Spawned puck !{puck.NetworkObjectId}!");
		return puck;
	}

	public void Server_DespawnPuck(Puck puck)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			puck.NetworkObject.Despawn();
			Debug.Log($"[PuckManager] Despawned puck !{puck.NetworkObjectId}!");
		}
	}

	public void Server_DespawnPucks(bool includeReplay = false)
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return;
		}
		Debug.Log($"[PuckManager] Despawning {pucks.Count} pucks, includeReplay: {includeReplay}");
		foreach (Puck item in pucks.ToList())
		{
			if (includeReplay || !item.IsReplay.Value)
			{
				Server_DespawnPuck(item);
			}
		}
	}

	public void Server_SpawnPucksForPhase(GamePhase phase)
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return;
		}
		Debug.Log($"[PuckManager] Spawning {puckPositions.Count} pucks for phase {phase}");
		foreach (PuckPosition puckPosition in puckPositions)
		{
			if (puckPosition.Phase == phase)
			{
				Server_SpawnPuck(puckPosition.transform.position, puckPosition.transform.rotation, Vector3.zero);
			}
		}
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		base.__initializeRpcs();
	}

	protected internal override string __getTypeName()
	{
		return "PuckManager";
	}
}
