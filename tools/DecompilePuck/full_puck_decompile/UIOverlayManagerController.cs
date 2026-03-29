using System.Collections.Generic;
using SocketIOClient;
using Unity.Netcode;
using UnityEngine;

internal class UIOverlayManagerController : MonoBehaviour
{
	private UIOverlayManager uiOverlay;

	private void Awake()
	{
		uiOverlay = GetComponent<UIOverlayManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnClientConnected", Event_OnClientConnected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnBaseCameraDisabled", Event_Client_OnBaseCameraDisabled);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPopupShow", Event_Client_OnPopupShow);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPopupHide", Event_Client_OnPopupHide);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("player", WebSocket_Event_OnPlayer);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("playerStartPurchaseResponse", WebSocket_Event_OnPlayerStartPurchaseResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("playerCompletePurchaseResponse", WebSocket_Event_OnPlayerCompletePurchaseResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("emit", WebSocket_Event_OnEmit);
		uiOverlay.ShowOverlay("loading", showSpinner: true);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnClientConnected", Event_OnClientConnected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnBaseCameraDisabled", Event_Client_OnBaseCameraDisabled);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPopupShow", Event_Client_OnPopupShow);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPopupHide", Event_Client_OnPopupHide);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("player", WebSocket_Event_OnPlayer);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("playerStartPurchaseResponse", WebSocket_Event_OnPlayerStartPurchaseResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("playerCompletePurchaseResponse", WebSocket_Event_OnPlayerCompletePurchaseResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("emit", WebSocket_Event_OnEmit);
	}

	private void Event_OnClientConnected(Dictionary<string, object> message)
	{
		ulong num = (ulong)message["clientId"];
		if (NetworkManager.Singleton.LocalClientId == num)
		{
			uiOverlay.HideOverlay("connecting", fade: true);
		}
	}

	private void Event_Client_OnBaseCameraDisabled(Dictionary<string, object> message)
	{
		uiOverlay.ShowOverlay("camera", showSpinner: false, fade: false, autoHide: true, autoHideFade: true);
	}

	private void Event_Client_OnClientStarted(Dictionary<string, object> message)
	{
		uiOverlay.ShowOverlay("connecting", showSpinner: true);
	}

	private void Event_Client_OnClientStopped(Dictionary<string, object> message)
	{
		uiOverlay.HideOverlay("connecting", fade: true);
	}

	private void Event_Client_OnPopupShow(Dictionary<string, object> message)
	{
		string text = (string)message["name"];
		if (!(text == "missingPassword"))
		{
			if (text == "pendingMods")
			{
				uiOverlay.ShowOverlay("pendingMods", showSpinner: true);
			}
		}
		else
		{
			uiOverlay.ShowOverlay("missingPassword", showSpinner: true);
		}
	}

	private void Event_Client_OnPopupHide(Dictionary<string, object> message)
	{
		string text = (string)message["name"];
		if (!(text == "missingPassword"))
		{
			if (text == "pendingMods")
			{
				uiOverlay.HideOverlay("pendingMods", fade: true);
			}
		}
		else
		{
			uiOverlay.HideOverlay("missingPassword", fade: true);
		}
	}

	private void WebSocket_Event_OnPlayer(Dictionary<string, object> message)
	{
		uiOverlay.HideOverlay("loading", fade: true);
	}

	private void WebSocket_Event_OnPlayerStartPurchaseResponse(Dictionary<string, object> message)
	{
		if (((SocketIOResponse)message["response"]).GetValue<PlayerStartPurchaseResponse>().success)
		{
			uiOverlay.ShowOverlay("purchase", showSpinner: false, fade: true);
		}
	}

	private void WebSocket_Event_OnPlayerCompletePurchaseResponse(Dictionary<string, object> message)
	{
		uiOverlay.HideOverlay("purchase", fade: true);
	}

	private void WebSocket_Event_OnEmit(Dictionary<string, object> message)
	{
		if ((string)message["messageName"] == "playerCancelPurchaseRequest")
		{
			uiOverlay.HideOverlay("purchase", fade: true);
		}
	}
}
