using System.Collections.Generic;
using UnityEngine;

public class SpectatorController : MonoBehaviour
{
	private Spectator spectator;

	private void Awake()
	{
		spectator = GetComponent<Spectator>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPuckSpawned", Event_OnPuckSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPuckSpawned", Event_OnPuckSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
	}

	private void Event_OnPuckSpawned(Dictionary<string, object> message)
	{
		Puck puck = (Puck)message["puck"];
		spectator.LookTarget = puck.transform;
	}

	private void Event_OnGamePhaseChanged(Dictionary<string, object> message)
	{
		switch ((GamePhase)message["newGamePhase"])
		{
		case GamePhase.BlueScore:
		case GamePhase.RedScore:
			spectator.PlayAnimation("Cheering", Random.Range(0f, 0.25f));
			break;
		case GamePhase.GameOver:
			spectator.PlayAnimation("Cheering", Random.Range(0f, 0.25f));
			break;
		default:
			spectator.PlayAnimation("Seated", Random.Range(0f, 0.25f));
			break;
		}
	}
}
