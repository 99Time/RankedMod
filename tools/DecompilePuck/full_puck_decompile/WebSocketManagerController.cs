using UnityEngine;

public class WebSocketManagerController : MonoBehaviour
{
	private WebSocketManager webSocketManager;

	private void Awake()
	{
		webSocketManager = GetComponent<WebSocketManager>();
	}

	private void Start()
	{
	}

	private void OnDestroy()
	{
	}
}
