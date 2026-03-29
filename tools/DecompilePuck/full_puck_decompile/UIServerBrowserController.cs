using System.Collections.Generic;
using System.Linq;
using SocketIOClient;
using UnityEngine;

public class UIServerBrowserController : MonoBehaviour
{
	private UIServerBrowser uiServerBrowser;

	private void Awake()
	{
		uiServerBrowser = GetComponent<UIServerBrowser>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("playerGetServerBrowserServersResponse", WebSocket_Event_OnPlayerGetServerBrowserServersResponse);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("playerGetServerBrowserServersResponse", WebSocket_Event_OnPlayerGetServerBrowserServersResponse);
	}

	private void WebSocket_Event_OnPlayerGetServerBrowserServersResponse(Dictionary<string, object> message)
	{
		ServerBrowserServersResponse value = ((SocketIOResponse)message["response"]).GetValue<ServerBrowserServersResponse>();
		uiServerBrowser.ClearServers();
		uiServerBrowser.UpdateServers(value.servers.ToList());
	}
}
