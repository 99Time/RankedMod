using System.Collections.Generic;
using Unity.Netcode;

public class PuckManagerController : NetworkBehaviour
{
	private PuckManager puckManager;

	private void Awake()
	{
		puckManager = GetComponent<PuckManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnLevelStarted", Event_OnLevelStarted);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPuckSpawned", Event_OnPuckSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPuckDespawned", Event_OnPuckDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
	}

	public override void OnNetworkDespawn()
	{
		puckManager.ClearPuckPositions();
		base.OnNetworkDespawn();
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnLevelStarted", Event_OnLevelStarted);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPuckSpawned", Event_OnPuckSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPuckDespawned", Event_OnPuckDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
		base.OnDestroy();
	}

	private void Event_OnLevelStarted(Dictionary<string, object> message)
	{
		List<PuckPosition> puckPositions = (List<PuckPosition>)message["puckPositions"];
		puckManager.SetPuckPositions(puckPositions);
	}

	private void Event_OnPuckSpawned(Dictionary<string, object> message)
	{
		Puck puck = (Puck)message["puck"];
		puckManager.AddPuck(puck);
	}

	private void Event_OnPuckDespawned(Dictionary<string, object> message)
	{
		Puck puck = (Puck)message["puck"];
		puckManager.RemovePuck(puck);
	}

	private void Event_OnGamePhaseChanged(Dictionary<string, object> message)
	{
		GamePhase gamePhase = (GamePhase)message["newGamePhase"];
		if (NetworkManager.Singleton.IsServer)
		{
			switch (gamePhase)
			{
			case GamePhase.Warmup:
			case GamePhase.FaceOff:
				puckManager.Server_DespawnPucks();
				break;
			case GamePhase.Replay:
				puckManager.Server_DespawnPucks();
				break;
			}
			puckManager.Server_SpawnPucksForPhase(gamePhase);
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
		return "PuckManagerController";
	}
}
