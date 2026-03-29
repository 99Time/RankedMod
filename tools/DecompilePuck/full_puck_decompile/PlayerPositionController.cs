using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerPositionController : MonoBehaviour
{
	private PlayerPosition playerPosition;

	private void Awake()
	{
		playerPosition = GetComponent<PlayerPosition>();
	}

	public void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerDespawned", Event_OnPlayerDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
	}

	public void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerDespawned", Event_OnPlayerDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
	}

	private void Event_OnPlayerDespawned(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (NetworkManager.Singleton.IsServer && player == playerPosition.ClaimedBy)
		{
			playerPosition.Server_Unclaim();
		}
	}

	private void Event_OnPlayerTeamChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (NetworkManager.Singleton.IsServer && player == playerPosition.ClaimedBy)
		{
			playerPosition.Server_Unclaim();
		}
	}
}
