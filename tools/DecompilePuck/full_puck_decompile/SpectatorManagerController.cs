using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SpectatorManagerController : NetworkBehaviour
{
	private SpectatorManager spectatorManager;

	private void Awake()
	{
		spectatorManager = GetComponent<SpectatorManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnLevelStarted", Event_OnLevelStarted);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnLevelDestroyed", Event_OnLevelDestroyed);
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnLevelStarted", Event_OnLevelStarted);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnLevelDestroyed", Event_OnLevelDestroyed);
		base.OnDestroy();
	}

	private void Event_OnLevelStarted(Dictionary<string, object> message)
	{
		List<Transform> spectatorPositions = (List<Transform>)message["spectatorPositions"];
		spectatorManager.SetSpectatorPositions(spectatorPositions);
		spectatorManager.SpawnSpectators();
	}

	private void Event_OnLevelDestroyed(Dictionary<string, object> message)
	{
		spectatorManager.ClearSpectators();
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
		return "SpectatorManagerController";
	}
}
