using System.Collections.Generic;
using SocketIOClient;
using UnityEngine;

public class UIServerLauncherController : MonoBehaviour
{
	private UIServerLauncher uIServerLauncher;

	private void Awake()
	{
		uIServerLauncher = GetComponent<UIServerLauncher>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerDataChanged", Event_Client_OnPlayerDataChanged);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("playerGetServerLauncherLocationsResponse", WebSocket_Event_OnPlayerGetServerLauncherLocationsResponse);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerDataChanged", Event_Client_OnPlayerDataChanged);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("playerGetServerLauncherLocationsResponse", WebSocket_Event_OnPlayerGetServerLauncherLocationsResponse);
	}

	private void Event_Client_OnPlayerDataChanged(Dictionary<string, object> message)
	{
		if (((PlayerData)message["newPlayerData"]).patreonLevel >= 1)
		{
			uIServerLauncher.ShowDedicatedPasswordProtection();
		}
		else
		{
			uIServerLauncher.HideDedicatedPasswordProtection();
		}
	}

	private void WebSocket_Event_OnPlayerGetServerLauncherLocationsResponse(Dictionary<string, object> message)
	{
		ServerLauncherLocationsResponse value = ((SocketIOResponse)message["response"]).GetValue<ServerLauncherLocationsResponse>();
		uIServerLauncher.SetDedicatedLocations(value.locations);
	}
}
