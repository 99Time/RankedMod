using System.Collections.Generic;
using Unity.Netcode;

public class ReplayRecorderController : NetworkBehaviour
{
	private ReplayRecorder replayRecorder;

	private void Awake()
	{
		replayRecorder = GetComponent<ReplayRecorder>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerDespawned", Event_OnPlayerDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerBodyDespawned", Event_OnPlayerBodyDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnStickSpawned", Event_OnStickSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnStickDespawned", Event_OnStickDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPuckSpawned", Event_OnPuckSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPuckDespawned", Event_OnPuckDespawned);
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerDespawned", Event_OnPlayerDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerBodyDespawned", Event_OnPlayerBodyDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnStickSpawned", Event_OnStickSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnStickDespawned", Event_OnStickDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPuckSpawned", Event_OnPuckSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPuckDespawned", Event_OnPuckDespawned);
		base.OnDestroy();
	}

	private void Event_OnPlayerSpawned(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (NetworkManager.Singleton.IsServer && !player.IsReplay.Value)
		{
			replayRecorder.Server_AddPlayerSpawnedEvent(player);
		}
	}

	private void Event_OnPlayerDespawned(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (NetworkManager.Singleton.IsServer && !player.IsReplay.Value)
		{
			replayRecorder.Server_AddPlayerDespawnedEvent(player);
		}
	}

	private void Event_OnPlayerBodySpawned(Dictionary<string, object> message)
	{
		PlayerBodyV2 playerBodyV = (PlayerBodyV2)message["playerBody"];
		if (NetworkManager.Singleton.IsServer && (bool)playerBodyV && !playerBodyV.Player.IsReplay.Value)
		{
			replayRecorder.Server_AddPlayerBodySpawnedEvent(playerBodyV);
		}
	}

	private void Event_OnPlayerBodyDespawned(Dictionary<string, object> message)
	{
		PlayerBodyV2 playerBodyV = (PlayerBodyV2)message["playerBody"];
		if (NetworkManager.Singleton.IsServer && !playerBodyV.Player.IsReplay.Value)
		{
			replayRecorder.Server_AddPlayerBodyDespawnedEvent(playerBodyV);
		}
	}

	private void Event_OnStickSpawned(Dictionary<string, object> message)
	{
		Stick stick = (Stick)message["stick"];
		if (NetworkManager.Singleton.IsServer && !stick.Player.IsReplay.Value)
		{
			replayRecorder.Server_AddStickSpawnedEvent(stick);
		}
	}

	private void Event_OnStickDespawned(Dictionary<string, object> message)
	{
		Stick stick = (Stick)message["stick"];
		if (NetworkManager.Singleton.IsServer && !stick.Player.IsReplay.Value)
		{
			replayRecorder.Server_AddStickDespawnedEvent(stick);
		}
	}

	private void Event_OnPuckSpawned(Dictionary<string, object> message)
	{
		Puck puck = (Puck)message["puck"];
		if (NetworkManager.Singleton.IsServer && !puck.IsReplay.Value)
		{
			replayRecorder.Server_AddPuckSpawnedEvent(puck);
		}
	}

	private void Event_OnPuckDespawned(Dictionary<string, object> message)
	{
		Puck puck = (Puck)message["puck"];
		if (NetworkManager.Singleton.IsServer && !puck.IsReplay.Value)
		{
			replayRecorder.Server_AddPuckDespawnedEvent(puck);
		}
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
		return "ReplayRecorderController";
	}
}
