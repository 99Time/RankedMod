using System.Collections.Generic;
using Unity.Netcode;

public class PlayerVoiceRecorderController : NetworkBehaviour
{
	private PlayerVoiceRecorder playerVoiceRecorder;

	private void Awake()
	{
		playerVoiceRecorder = GetComponent<PlayerVoiceRecorder>();
	}

	private void Start()
	{
		playerVoiceRecorder.IsEnabled = NetworkBehaviourSingleton<ServerManager>.Instance.Server.Voip;
	}

	public override void OnNetworkSpawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerTalkInput", Event_OnPlayerTalkInput);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnServerConfiguration", Event_Client_OnServerConfiguration);
		base.OnNetworkSpawn();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerTalkInput", Event_OnPlayerTalkInput);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnServerConfiguration", Event_Client_OnServerConfiguration);
		base.OnNetworkDespawn();
	}

	private void Event_OnPlayerTalkInput(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		bool flag = (bool)message["value"];
		if (base.OwnerClientId == player.OwnerClientId && player.IsLocalPlayer)
		{
			if (flag)
			{
				playerVoiceRecorder.StartRecording();
			}
			else
			{
				playerVoiceRecorder.StopRecording();
			}
		}
	}

	private void Event_Client_OnServerConfiguration(Dictionary<string, object> message)
	{
		Server server = (Server)message["server"];
		playerVoiceRecorder.IsEnabled = server.Voip;
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
		return "PlayerVoiceRecorderController";
	}
}
