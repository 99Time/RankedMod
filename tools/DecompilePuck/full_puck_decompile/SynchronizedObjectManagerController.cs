using System.Collections.Generic;
using UnityEngine;

public class SynchronizedObjectManagerController : MonoBehaviour
{
	private SynchronizedObjectManager synchronizedObjectManager;

	private void Awake()
	{
		synchronizedObjectManager = GetComponent<SynchronizedObjectManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnSynchronizedObjectSpawned", Event_OnSynchronizedObjectSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnSynchronizedObjectDespawned", Event_OnSynchronizedObjectDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerDespawned", Event_OnPlayerDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnSynchronizeComplete", Event_Server_OnSynchronizeComplete);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnUseNetworkSmoothingChanged", Event_Client_OnUseNetworkSmoothingChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnNetworkSmoothingStrengthChanged", Event_Client_OnNetworkSmoothingStrengthChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
		synchronizedObjectManager.UseNetworkSmoothing = MonoBehaviourSingleton<SettingsManager>.Instance.UseNetworkSmoothing > 0;
		synchronizedObjectManager.NetworkSmoothingStrength = MonoBehaviourSingleton<SettingsManager>.Instance.NetworkSmoothingStrength;
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnSynchronizedObjectSpawned", Event_OnSynchronizedObjectSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnSynchronizedObjectDespawned", Event_OnSynchronizedObjectDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerDespawned", Event_OnPlayerDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnSynchronizeComplete", Event_Server_OnSynchronizeComplete);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnUseNetworkSmoothingChanged", Event_Client_OnUseNetworkSmoothingChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnNetworkSmoothingStrengthChanged", Event_Client_OnNetworkSmoothingStrengthChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
		synchronizedObjectManager.Dispose();
	}

	private void Event_OnSynchronizedObjectSpawned(Dictionary<string, object> message)
	{
		SynchronizedObject synchronizedObject = (SynchronizedObject)message["synchronizedObject"];
		synchronizedObjectManager.AddSynchronizedObject(synchronizedObject);
	}

	private void Event_OnSynchronizedObjectDespawned(Dictionary<string, object> message)
	{
		SynchronizedObject synchronizedObject = (SynchronizedObject)message["synchronizedObject"];
		synchronizedObjectManager.RemoveSynchronizedObject(synchronizedObject);
	}

	private void Event_OnPlayerSpawned(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (player.OwnerClientId != 0L)
		{
			synchronizedObjectManager.Server_AddSynchronizedClientId(player.OwnerClientId);
		}
	}

	private void Event_OnPlayerDespawned(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		synchronizedObjectManager.Server_RemoveSynchronizedClientId(player.OwnerClientId);
	}

	private void Event_Server_OnSynchronizeComplete(Dictionary<string, object> message)
	{
		ulong clientId = (ulong)message["clientId"];
		synchronizedObjectManager.Server_ForceSynchronizeClientId(clientId);
	}

	private void Event_Client_OnUseNetworkSmoothingChanged(Dictionary<string, object> message)
	{
		bool useNetworkSmoothing = (bool)message["value"];
		synchronizedObjectManager.UseNetworkSmoothing = useNetworkSmoothing;
	}

	private void Event_Client_OnNetworkSmoothingStrengthChanged(Dictionary<string, object> message)
	{
		float networkSmoothingStrength = (float)message["value"];
		synchronizedObjectManager.NetworkSmoothingStrength = networkSmoothingStrength;
	}

	private void Event_Client_OnClientStopped(Dictionary<string, object> message)
	{
		synchronizedObjectManager.Dispose();
	}
}
