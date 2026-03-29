using System.Collections.Generic;
using SocketIOClient;
using Unity.Netcode;
using UnityEngine;

public class UIToastManagerController : MonoBehaviour
{
	private UIToastManager uiToastManager;

	private void Awake()
	{
		uiToastManager = GetComponent<UIToastManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnClientConnected", Event_OnClientConnected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnGetAuthTicketForWebApi", Event_Client_OnGetAuthTicketForWebApi);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnGotAuthTicketForWebApi", Event_Client_OnGotAuthTicketForWebApi);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnServerStartFailed", Event_Client_OnServerStartFailed);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnConnectionRejected", Event_Client_OnConnectionRejected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnDisconnected", Event_Client_OnDisconnected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPendingModsReset", Event_Client_OnPendingModsReset);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("emit", WebSocket_Event_OnEmit);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("playerAuthenticateResponse", WebSocket_Event_OnPlayerAuthenticateResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("playerStartPurchaseResponse", WebSocket_Event_OnPlayerStartPurchaseResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("playerCompletePurchaseResponse", WebSocket_Event_OnPlayerCompletePurchaseResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("playerLaunchServerResponse", WebSocket_Event_OnPlayerLaunchServerResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("playerSetIdentityResponse", WebSocket_Event_OnPlayerSetIdentityResponse);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnClientConnected", Event_OnClientConnected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnGetAuthTicketForWebApi", Event_Client_OnGetAuthTicketForWebApi);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnGotAuthTicketForWebApi", Event_Client_OnGotAuthTicketForWebApi);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnServerStartFailed", Event_Client_OnServerStartFailed);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnConnectionRejected", Event_Client_OnConnectionRejected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnDisconnected", Event_Client_OnDisconnected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPendingModsReset", Event_Client_OnPendingModsReset);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("emit", WebSocket_Event_OnEmit);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("playerAuthenticateResponse", WebSocket_Event_OnPlayerAuthenticateResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("playerStartPurchaseResponse", WebSocket_Event_OnPlayerStartPurchaseResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("playerCompletePurchaseResponse", WebSocket_Event_OnPlayerCompletePurchaseResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("playerLaunchServerResponse", WebSocket_Event_OnPlayerLaunchServerResponse);
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("playerSetIdentityResponse", WebSocket_Event_OnPlayerSetIdentityResponse);
	}

	private void Event_OnClientConnected(Dictionary<string, object> message)
	{
		ulong num = (ulong)message["clientId"];
		if (NetworkManager.Singleton.LocalClientId == num)
		{
			uiToastManager.HideToast("connectingToServer");
		}
	}

	private void Event_Client_OnGetAuthTicketForWebApi(Dictionary<string, object> message)
	{
		uiToastManager.ShowToast("connectingToSteam", "Connecting to Steam...", float.PositiveInfinity);
	}

	private void Event_Client_OnGotAuthTicketForWebApi(Dictionary<string, object> message)
	{
		uiToastManager.HideToast("connectingToSteam");
	}

	private void Event_Client_OnServerStartFailed(Dictionary<string, object> message)
	{
		bool num = (bool)message["isHost"];
		bool flag = (bool)message["isServer"];
		if (num)
		{
			uiToastManager.ShowToast("serverStartFailed", "Host start failure: Transport failure");
		}
		else if (flag)
		{
			uiToastManager.ShowToast("serverStartFailed", "Server start failure: Transport failure");
		}
	}

	private void Event_Client_OnClientStarted(Dictionary<string, object> message)
	{
		uiToastManager.ShowToast("connectingToServer", "Connecting to server...", float.PositiveInfinity);
	}

	private void Event_Client_OnClientStopped(Dictionary<string, object> message)
	{
		uiToastManager.HideToast("connectingToServer");
	}

	private void Event_Client_OnConnectionRejected(Dictionary<string, object> message)
	{
		ConnectionRejection connectionRejection = (ConnectionRejection)message["connectionRejection"];
		if (connectionRejection.code != ConnectionRejectionCode.MissingPassword && connectionRejection.code != ConnectionRejectionCode.MissingMods)
		{
			uiToastManager.HideToast("connectingToServer");
			uiToastManager.ShowToast("connectionRejection", "Connection failure: " + Utils.GetConnectionRejectionMessage(connectionRejection.code));
		}
	}

	private void Event_Client_OnDisconnected(Dictionary<string, object> message)
	{
		Disconnection disconnection = (Disconnection)message["disconnection"];
		if (disconnection.code != DisconnectionCode.Disconnected)
		{
			uiToastManager.ShowToast("disconnection", "Disconnected: " + Utils.GetDisconnectionMessage(disconnection.code));
		}
	}

	private void Event_Client_OnPendingModsReset(Dictionary<string, object> message)
	{
		string text = (string)message["reason"];
		uiToastManager.ShowToast("pendingModsReset", text ?? "");
	}

	private void WebSocket_Event_OnEmit(Dictionary<string, object> message)
	{
		switch ((string)message["messageName"])
		{
		case "playerAuthenticateRequest":
			uiToastManager.ShowToast("playerAuthenticate", "Authenticating with Puck...", float.PositiveInfinity);
			break;
		case "playerStartPurchaseRequest":
			uiToastManager.ShowToast("playerStartPurchase", "Starting transaction...", float.PositiveInfinity);
			break;
		case "playerCompletePurchaseRequest":
			uiToastManager.ShowToast("playerCompletePurchase", "Completing transaction...", float.PositiveInfinity);
			break;
		case "playerCancelPurchaseRequest":
			uiToastManager.ShowToast("playerCancelPurchase", "Transaction cancelled.");
			break;
		case "playerLaunchServerRequest":
			uiToastManager.ShowToast("playerLaunchServer", "Launching server...", float.PositiveInfinity);
			break;
		case "playerSetIdentityRequest":
			uiToastManager.ShowToast("playerSetIdentity", "Setting identity...", float.PositiveInfinity);
			break;
		}
	}

	private void WebSocket_Event_OnPlayerAuthenticateResponse(Dictionary<string, object> message)
	{
		PlayerAuthenticateResponse value = ((SocketIOResponse)message["response"]).GetValue<PlayerAuthenticateResponse>();
		if (value.success)
		{
			uiToastManager.HideToast("playerAuthenticate");
		}
		else
		{
			uiToastManager.ShowToast("playerAuthenticate", "Authentication failure: " + value.error);
		}
	}

	private void WebSocket_Event_OnPlayerStartPurchaseResponse(Dictionary<string, object> message)
	{
		PlayerStartPurchaseResponse value = ((SocketIOResponse)message["response"]).GetValue<PlayerStartPurchaseResponse>();
		if (value.success)
		{
			uiToastManager.HideToast("playerStartPurchase");
		}
		else
		{
			uiToastManager.ShowToast("playerStartPurchase", "Transaction failure: " + value.error);
		}
	}

	private void WebSocket_Event_OnPlayerCompletePurchaseResponse(Dictionary<string, object> message)
	{
		PlayerCompletePurchaseResponse value = ((SocketIOResponse)message["response"]).GetValue<PlayerCompletePurchaseResponse>();
		if (value.success)
		{
			uiToastManager.HideToast("playerCompletePurchase");
		}
		else
		{
			uiToastManager.ShowToast("playerCompletePurchase", "Transaction failure: " + value.error);
		}
	}

	private void WebSocket_Event_OnPlayerLaunchServerResponse(Dictionary<string, object> message)
	{
		PlayerLaunchServerResponse value = ((SocketIOResponse)message["response"]).GetValue<PlayerLaunchServerResponse>();
		if (value.success)
		{
			uiToastManager.ShowToast("playerLaunchServer", "Server launched!");
		}
		else
		{
			uiToastManager.ShowToast("playerLaunchServer", "Server launch failure: " + value.error);
		}
	}

	private void WebSocket_Event_OnPlayerSetIdentityResponse(Dictionary<string, object> message)
	{
		PlayerSetIdentityResponse value = ((SocketIOResponse)message["response"]).GetValue<PlayerSetIdentityResponse>();
		if (value.success)
		{
			uiToastManager.ShowToast("playerSetIdentity", "Identity set!");
		}
		else
		{
			uiToastManager.ShowToast("playerSetIdentity", "Identity set failure: " + value.error);
		}
	}
}
