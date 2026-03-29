using System.Collections.Generic;
using Unity.Netcode;

public class GoalController : NetworkBehaviour
{
	private Goal goal;

	private void Awake()
	{
		goal = GetComponent<Goal>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPuckSpawned", Event_OnPuckSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPuckDespawned", Event_OnPuckDespawned);
		NetworkBehaviourSingleton<PuckManager>.Instance.GetPucks().ForEach(delegate(Puck puck)
		{
			goal.Client_AddNetClothSphereCollider(puck.NetSphereCollider);
		});
	}

	public override void OnNetworkSpawn()
	{
		goal.NetCloth.enabled = NetworkManager.Singleton.IsClient;
		base.OnNetworkSpawn();
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPuckSpawned", Event_OnPuckSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPuckDespawned", Event_OnPuckDespawned);
		base.OnDestroy();
	}

	private void Event_OnPuckSpawned(Dictionary<string, object> message)
	{
		Puck puck = (Puck)message["puck"];
		if (NetworkManager.Singleton.IsClient)
		{
			goal.Client_AddNetClothSphereCollider(puck.NetSphereCollider);
		}
	}

	private void Event_OnPuckDespawned(Dictionary<string, object> message)
	{
		Puck puck = (Puck)message["puck"];
		if (NetworkManager.Singleton.IsClient)
		{
			goal.Client_RemoveNetClothSphereCollider(puck.NetSphereCollider);
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
		return "GoalController";
	}
}
