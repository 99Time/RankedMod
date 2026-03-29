using System.Collections.Generic;
using Unity.Netcode;

public class ReplayManagerController : NetworkBehaviour
{
	private ReplayManager replayManager;

	private void Awake()
	{
		replayManager = GetComponent<ReplayManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnServerStopped", Event_Server_OnServerStopped);
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnServerStopped", Event_Server_OnServerStopped);
		base.OnDestroy();
	}

	private void Event_OnGamePhaseChanged(Dictionary<string, object> message)
	{
		GamePhase gamePhase = (GamePhase)message["newGamePhase"];
		int num = (int)message["time"];
		if (NetworkManager.Singleton.IsServer)
		{
			switch (gamePhase)
			{
			case GamePhase.Warmup:
				replayManager.Server_StopRecording();
				replayManager.Server_StopReplaying();
				break;
			case GamePhase.FaceOff:
				replayManager.Server_StopReplaying();
				replayManager.Server_StartRecording();
				break;
			case GamePhase.Replay:
				replayManager.Server_StopRecording();
				replayManager.Server_StartReplaying(num);
				break;
			default:
				replayManager.Server_StopRecording();
				replayManager.Server_StopReplaying();
				break;
			case GamePhase.Playing:
			case GamePhase.BlueScore:
			case GamePhase.RedScore:
				break;
			}
		}
	}

	private void Event_Server_OnServerStopped(Dictionary<string, object> message)
	{
		replayManager.Server_StopReplaying();
		replayManager.Server_StopRecording();
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
		return "ReplayManagerController";
	}
}
