using System.Collections.Generic;
using UnityEngine;

public class SceneManagerController : MonoBehaviour
{
	private SceneManager sceneManager;

	private void Awake()
	{
		sceneManager = GetComponent<SceneManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnBeforeServerStarted", Event_Server_OnBeforeServerStarted);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnTransportFailure", Event_Client_OnTransportFailure);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnBeforeServerStarted", Event_Server_OnBeforeServerStarted);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnTransportFailure", Event_Client_OnTransportFailure);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
	}

	private void Event_Server_OnBeforeServerStarted(Dictionary<string, object> message)
	{
		sceneManager.LoadLevel1Scene();
	}

	private void Event_Client_OnTransportFailure(Dictionary<string, object> message)
	{
		sceneManager.LoadChangingRoomScene();
	}

	private void Event_Client_OnClientStopped(Dictionary<string, object> message)
	{
		sceneManager.LoadChangingRoomScene();
	}
}
