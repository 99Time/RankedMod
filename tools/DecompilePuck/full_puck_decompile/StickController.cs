using System.Collections.Generic;
using Unity.Netcode;

public class StickController : NetworkBehaviour
{
	private Stick stick;

	private void Awake()
	{
		stick = GetComponent<Stick>();
	}

	public override void OnNetworkSpawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerStickSkinChanged", Event_OnPlayerStickSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerStickShaftTapeSkinChanged", Event_OnPlayerStickShaftTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerStickBladeTapeSkinChanged", Event_OnPlayerStickBladeTapeSkinChanged);
		base.OnNetworkSpawn();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerStickSkinChanged", Event_OnPlayerStickSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerStickShaftTapeSkinChanged", Event_OnPlayerStickShaftTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerStickBladeTapeSkinChanged", Event_OnPlayerStickBladeTapeSkinChanged);
		base.OnNetworkDespawn();
	}

	private void Event_OnPlayerRoleChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			stick.UpdateStick();
		}
	}

	private void Event_OnPlayerTeamChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			stick.UpdateStick();
		}
	}

	private void Event_OnPlayerStickSkinChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			stick.UpdateStick();
		}
	}

	private void Event_OnPlayerStickShaftTapeSkinChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			stick.UpdateStick();
		}
	}

	private void Event_OnPlayerStickBladeTapeSkinChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (base.OwnerClientId == player.OwnerClientId)
		{
			stick.UpdateStick();
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
		return "StickController";
	}
}
