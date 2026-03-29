using System.Collections.Generic;
using Unity.Netcode;

public class StickPositionerController : NetworkBehaviour
{
	private StickPositioner stickPositioner;

	private void Awake()
	{
		stickPositioner = GetComponent<StickPositioner>();
	}

	public override void OnNetworkSpawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnStickSpawned", Event_OnStickSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerHandednessChanged", Event_OnPlayerHandednessChanged);
		base.OnNetworkSpawn();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnStickSpawned", Event_OnStickSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerHandednessChanged", Event_OnPlayerHandednessChanged);
		base.OnNetworkDespawn();
	}

	private void Event_OnStickSpawned(Dictionary<string, object> message)
	{
		Stick stick = (Stick)message["stick"];
		if (base.OwnerClientId == stick.OwnerClientId)
		{
			stickPositioner.PrepareShaftTarget(stick);
		}
	}

	private void Event_OnPlayerHandednessChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		PlayerHandedness handedness = (PlayerHandedness)message["newHandedness"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			stickPositioner.Handedness = handedness;
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
		return "StickPositionerController";
	}
}
