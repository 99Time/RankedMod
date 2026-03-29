using System.Collections.Generic;
using Unity.Netcode;

public class PlayerInputController : NetworkBehaviour
{
	private PlayerInput playerInput;

	private void Awake()
	{
		playerInput = GetComponent<PlayerInput>();
	}

	private void Start()
	{
		playerInput.InitialLookAngle = MonoBehaviourSingleton<SettingsManager>.Instance.CameraAngle;
	}

	public override void OnNetworkSpawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerHandednessChanged", Event_OnPlayerHandednessChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnSynchronizeComplete", Event_Server_OnSynchronizeComplete);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnServerConfiguration", Event_Client_OnServerConfiguration);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnCameraAngleChanged", Event_Client_OnCameraAngleChanged);
		base.OnNetworkSpawn();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerHandednessChanged", Event_OnPlayerHandednessChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnSynchronizeComplete", Event_Server_OnSynchronizeComplete);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnServerConfiguration", Event_Client_OnServerConfiguration);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnCameraAngleChanged", Event_Client_OnCameraAngleChanged);
		base.OnNetworkDespawn();
	}

	private void Event_OnPlayerBodySpawned(Dictionary<string, object> message)
	{
		if (((PlayerBodyV2)message["playerBody"]).Player.IsLocalPlayer)
		{
			playerInput.ResetInputs();
		}
	}

	private void Event_OnPlayerHandednessChanged(Dictionary<string, object> message)
	{
		if (((Player)message["player"]).IsLocalPlayer)
		{
			playerInput.ResetInputs(invertStickRaycastOriginAngle: true);
		}
	}

	private void Event_Server_OnSynchronizeComplete(Dictionary<string, object> message)
	{
		ulong clientId = (ulong)message["clientId"];
		playerInput.Server_ForceSynchronizeClientId(clientId);
	}

	private void Event_Client_OnServerConfiguration(Dictionary<string, object> message)
	{
		Server server = (Server)message["server"];
		playerInput.TickRate = server.ClientTickRate;
		playerInput.SleepTimeout = server.SleepTimeout;
	}

	private void Event_Client_OnCameraAngleChanged(Dictionary<string, object> message)
	{
		float initialLookAngle = (float)message["value"];
		playerInput.InitialLookAngle = initialLookAngle;
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
		return "PlayerInputController";
	}
}
