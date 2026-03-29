using System.Collections.Generic;
using System.Linq;
using SocketIOClient;
using UnityEngine;

public class ItemManagerController : MonoBehaviour
{
	private ItemManager itemManager;

	private void Awake()
	{
		itemManager = GetComponent<ItemManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<WebSocketManager>.Instance.AddMessageListener("player", WebSocket_Event_OnPlayer);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<WebSocketManager>.Instance.RemoveMessageListener("player", WebSocket_Event_OnPlayer);
	}

	private void WebSocket_Event_OnPlayer(Dictionary<string, object> message)
	{
		PlayerData value = ((SocketIOResponse)message["response"]).GetValue<PlayerData>();
		itemManager.SetItems(value.items.Select((PlayerItem item) => item.itemId).ToArray());
	}
}
