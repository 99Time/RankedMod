using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

internal class NetworkManagerEventEmitter : MonoBehaviour
{
	private bool isClient;

	private bool isServer;

	private void Start()
	{
		if (!(NetworkManager.Singleton == null))
		{
			NetworkManager.Singleton.OnServerStarted += Server_OnServerStarted;
			NetworkManager.Singleton.OnServerStopped += Server_OnServerStopped;
			NetworkManager.Singleton.OnClientStarted += Client_OnClientStarted;
			NetworkManager.Singleton.OnClientStopped += Client_OnClientStopped;
			NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
			NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
			NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
		}
	}

	private void OnDestroy()
	{
		if (!(NetworkManager.Singleton == null))
		{
			NetworkManager.Singleton.OnServerStarted -= Server_OnServerStarted;
			NetworkManager.Singleton.OnServerStopped -= Server_OnServerStopped;
			NetworkManager.Singleton.OnClientStarted -= Client_OnClientStarted;
			NetworkManager.Singleton.OnClientStopped -= Client_OnClientStopped;
			NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
			NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
			NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
		}
	}

	private void Update()
	{
		if ((bool)NetworkManager.Singleton)
		{
			if (isClient != NetworkManager.Singleton.IsClient)
			{
				isClient = NetworkManager.Singleton.IsClient;
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnIsClientChanged");
			}
			if (isServer != NetworkManager.Singleton.IsServer)
			{
				isServer = NetworkManager.Singleton.IsServer;
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnIsServerChanged");
			}
		}
	}

	private void Server_OnServerStarted()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnServerStarted");
		if (!(NetworkManager.Singleton == null))
		{
			NetworkManager.Singleton.SceneManager.OnSynchronizeComplete += Server_OnSynchronizeComplete;
			if (NetworkManager.Singleton.IsHost)
			{
				Server_OnSynchronizeComplete(0uL);
			}
		}
	}

	private void Server_OnServerStopped(bool wasHost)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnServerStopped", new Dictionary<string, object> { { "wasHost", wasHost } });
	}

	private void Client_OnClientStarted()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnClientStarted");
	}

	private void Client_OnClientStopped(bool wasHost)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnClientStopped", new Dictionary<string, object> { { "wasHost", wasHost } });
	}

	private void OnClientConnected(ulong clientId)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnClientConnected", new Dictionary<string, object> { { "clientId", clientId } });
	}

	private void OnClientDisconnected(ulong clientId)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnClientDisconnected", new Dictionary<string, object> { { "clientId", clientId } });
	}

	private void OnTransportFailure()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnTransportFailure");
	}

	private void Server_OnSynchronizeComplete(ulong clientId)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Server_OnSynchronizeComplete", new Dictionary<string, object> { { "clientId", clientId } });
	}
}
