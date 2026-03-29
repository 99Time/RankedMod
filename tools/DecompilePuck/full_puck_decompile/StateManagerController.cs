using System.Collections.Generic;
using SocketIOClient;
using UnityEngine;

public class StateManagerController : MonoBehaviour
{
	private StateManager stateManager;

	private string identityName;

	private int identityNumber;

	private void Awake()
	{
		stateManager = GetComponent<StateManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnIdentityNameChanged", Event_Client_OnIdentityNameChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnIdentityNumberChanged", Event_Client_OnIdentityNumberChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPopupClickOk", Event_Client_OnPopupClickOk);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("player", WebSocket_Event_OnPlayer);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnIdentityNameChanged", Event_Client_OnIdentityNameChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnIdentityNumberChanged", Event_Client_OnIdentityNumberChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPopupClickOk", Event_Client_OnPopupClickOk);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("player", WebSocket_Event_OnPlayer);
	}

	private void Event_Client_OnIdentityNameChanged(Dictionary<string, object> message)
	{
		string text = (string)message["value"];
		identityName = text;
	}

	private void Event_Client_OnIdentityNumberChanged(Dictionary<string, object> message)
	{
		int num = (int)message["value"];
		identityNumber = num;
	}

	private void Event_Client_OnPopupClickOk(Dictionary<string, object> message)
	{
		if (!((string)message["name"] != "identity"))
		{
			MonoBehaviourSingleton<WebSocketManager>.Instance.Emit("playerSetIdentityRequest", new Dictionary<string, object>
			{
				{ "username", identityName },
				{ "number", identityNumber }
			}, "playerSetIdentityResponse");
		}
	}

	private void WebSocket_Event_OnPlayer(Dictionary<string, object> message)
	{
		SocketIOResponse socketIOResponse = (SocketIOResponse)message["response"];
		stateManager.PlayerData = socketIOResponse.GetValue<PlayerData>();
	}
}
