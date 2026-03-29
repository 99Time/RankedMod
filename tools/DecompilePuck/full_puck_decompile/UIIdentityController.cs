using System.Collections.Generic;
using SocketIOClient;
using UnityEngine;

public class UIIdentityController : MonoBehaviour
{
	private UIIdentity uiIdentity;

	private void Awake()
	{
		uiIdentity = GetComponent<UIIdentity>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerDataChanged", Event_Client_OnPlayerDataChanged);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("playerSetIdentityResponse", WebSocket_Event_OnPlayerSetIdentityResponse);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerDataChanged", Event_Client_OnPlayerDataChanged);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("playerSetIdentityResponse", WebSocket_Event_OnPlayerSetIdentityResponse);
	}

	private void Event_Client_OnPlayerDataChanged(Dictionary<string, object> message)
	{
		uiIdentity.ApplyIdentityValues();
	}

	private void WebSocket_Event_OnPlayerSetIdentityResponse(Dictionary<string, object> message)
	{
		if (!((SocketIOResponse)message["response"]).GetValue<PlayerSetIdentityResponse>().success)
		{
			uiIdentity.ApplyIdentityValues();
		}
	}
}
