using System.Collections.Generic;
using UnityEngine;

public class SpectatorManager : NetworkBehaviourSingleton<SpectatorManager>
{
	[Header("Settings")]
	[SerializeField]
	private float spectatorDensity = 0.25f;

	[Header("Prefabs")]
	[SerializeField]
	private Spectator spectatorPrefab;

	private List<Transform> spectatorPositions = new List<Transform>();

	private List<Spectator> spectators = new List<Spectator>();

	public void SetSpectatorPositions(List<Transform> spectatorPositions)
	{
		this.spectatorPositions = spectatorPositions;
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnSpectatorPositionsSet");
	}

	public void SpawnSpectators()
	{
		if (Application.isBatchMode)
		{
			return;
		}
		foreach (Transform spectatorPosition in spectatorPositions)
		{
			if (Random.value < spectatorDensity)
			{
				Spectator item = Object.Instantiate(spectatorPrefab, spectatorPosition.position, spectatorPosition.rotation, spectatorPosition.parent);
				spectators.Add(item);
			}
		}
		Debug.Log($"[SpectatorManager] Spawned {spectators.Count} spectators");
	}

	public void ClearSpectators()
	{
		foreach (Spectator spectator in spectators)
		{
			Object.Destroy(spectator.gameObject);
		}
		spectators.Clear();
		Debug.Log("[SpectatorManager] Cleared spectators");
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
		return "SpectatorManager";
	}
}
