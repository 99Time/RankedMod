using System.Collections.Generic;
using Unity.Netcode;

public class PlayerManagerController : NetworkBehaviour
{
	private PlayerManager playerManager;

	private void Awake()
	{
		playerManager = GetComponent<PlayerManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnClientConnected", Event_OnClientConnected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerDespawned", Event_OnPlayerDespawned);
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnClientConnected", Event_OnClientConnected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerDespawned", Event_OnPlayerDespawned);
		base.OnDestroy();
	}

	private void Event_OnClientConnected(Dictionary<string, object> message)
	{
		ulong clientId = (ulong)message["clientId"];
		if (NetworkManager.Singleton.IsServer)
		{
			playerManager.Server_SpawnPlayer(clientId);
		}
	}

	private void Event_OnPlayerSpawned(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		playerManager.AddPlayer(player);
	}

	private void Event_OnPlayerDespawned(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		playerManager.RemovePlayer(player);
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		base.__initializeRpcs();
	}

	protected internal override string __getTypeName()
	{
		return "PlayerManagerController";
	}
}
