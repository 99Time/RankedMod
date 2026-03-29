using System.Collections.Generic;
using UnityEngine;

public class PuckPosition : MonoBehaviour
{
	public GamePhase Phase;

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPuckPositionSpawned", new Dictionary<string, object> { { "puckPosition", this } });
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnPuckPositionDespawned", new Dictionary<string, object> { { "puckPosition", this } });
	}
}
