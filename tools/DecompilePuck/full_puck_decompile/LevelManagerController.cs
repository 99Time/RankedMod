using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LevelManagerController : MonoBehaviour
{
	private LevelManager levelManager;

	private bool isBlackHoleActive;

	private Vector3 blackHolePosition;

	private float blackHoleBasePullStrength = 10f;

	private float blackHoleMinDistance = 0.5f;

	private void Awake()
	{
		levelManager = GetComponent<LevelManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerStateChanged", Event_OnPlayerStateChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnClientConnected", Event_OnClientConnected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Server_OnChatCommand", Event_Server_OnChatCommand);
		levelManager.Client_EnableObserverCamera();
		levelManager.Client_DeactivateGoalLights();
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerStateChanged", Event_OnPlayerStateChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnClientConnected", Event_OnClientConnected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Server_OnChatCommand", Event_Server_OnChatCommand);
	}

	private void FixedUpdate()
	{
		if (!isBlackHoleActive)
		{
			return;
		}
		foreach (Puck puck in NetworkBehaviourSingleton<PuckManager>.Instance.GetPucks())
		{
			if (!(puck == null))
			{
				Rigidbody component = puck.GetComponent<Rigidbody>();
				if (!(component == null))
				{
					Vector3 position = puck.transform.position;
					Vector3 normalized = (blackHolePosition - position).normalized;
					float a = Vector3.Distance(blackHolePosition, position);
					a = Mathf.Max(a, blackHoleMinDistance);
					float num = blackHoleBasePullStrength / a;
					Vector3 force = normalized * num;
					component.AddForce(force, ForceMode.Force);
				}
			}
		}
	}

	private void Event_OnPlayerStateChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		if (player.IsLocalPlayer)
		{
			switch (player.State.Value)
			{
			case PlayerState.TeamSelect:
				levelManager.Client_EnableObserverCamera();
				break;
			case PlayerState.PositionSelectBlue:
				levelManager.Client_EnableBluePositionSelectionCamera();
				break;
			case PlayerState.PositionSelectRed:
				levelManager.Client_EnableRedPositionSelectionCamera();
				break;
			case PlayerState.Replay:
				levelManager.Client_EnableReplayCamera();
				break;
			case PlayerState.Play:
				break;
			}
		}
	}

	private void Event_OnGamePhaseChanged(Dictionary<string, object> message)
	{
		GamePhase gamePhase = (GamePhase)message["newGamePhase"];
		if (NetworkManager.Singleton.IsServer && gamePhase != GamePhase.Warmup)
		{
			levelManager.PuckShooter.Server_StopShootingCoroutine();
			isBlackHoleActive = false;
		}
		switch (gamePhase)
		{
		case GamePhase.PeriodOver:
			levelManager.Server_PlayPeriodHornSound();
			break;
		case GamePhase.BlueScore:
			levelManager.Client_ActivateRedGoalLight();
			levelManager.Server_PlayTeamRedGoalSound();
			levelManager.Server_PlayCheerSound();
			break;
		case GamePhase.RedScore:
			levelManager.Client_ActivateBlueGoalLight();
			levelManager.Server_PlayTeamBlueGoalSound();
			levelManager.Server_PlayCheerSound();
			break;
		case GamePhase.GameOver:
			levelManager.Server_PlayPeriodHornSound();
			levelManager.Server_PlayCheerSound();
			break;
		default:
			levelManager.Client_DeactivateGoalLights();
			break;
		}
	}

	private void Event_OnClientConnected(Dictionary<string, object> message)
	{
		ulong num = (ulong)message["clientId"];
		if (NetworkManager.Singleton.LocalClientId == num)
		{
			levelManager.Client_EnableObserverCamera();
			levelManager.Client_DeactivateGoalLights();
		}
	}

	private void Event_Server_OnChatCommand(Dictionary<string, object> message)
	{
		ulong clientId = (ulong)message["clientId"];
		string text = (string)message["command"];
		string[] array = (string[])message["args"];
		Player playerByClientId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId);
		GamePhase phase = NetworkBehaviourSingleton<GameManager>.Instance.GameState.Value.Phase;
		switch (text)
		{
		case "/puckshooter":
			if (array.Length < 1)
			{
				levelManager.PuckShooter.Server_StartShootingCoroutine();
			}
			else if ((Application.isEditor || phase == GamePhase.Warmup) && (bool)playerByClientId && playerByClientId.IsCharacterFullySpawned)
			{
				if (array[0] == "on")
				{
					levelManager.PuckShooter.transform.position = playerByClientId.PlayerBody.transform.position;
					levelManager.PuckShooter.transform.rotation = playerByClientId.PlayerBody.transform.rotation;
					levelManager.PuckShooter.Server_StartShootingCoroutine();
				}
				else if (array[0] == "off")
				{
					levelManager.PuckShooter.Server_StopShootingCoroutine();
				}
			}
			break;
		case "/magnet":
			if ((Application.isEditor || phase == GamePhase.Warmup) && (bool)playerByClientId && playerByClientId.AdminLevel.Value >= 2 && playerByClientId.IsCharacterFullySpawned)
			{
				isBlackHoleActive = !isBlackHoleActive;
				if (isBlackHoleActive)
				{
					blackHolePosition = playerByClientId.Stick.BladeHandlePosition;
				}
			}
			break;
		case "/pucks":
			if ((Application.isEditor || phase == GamePhase.Warmup) && (bool)playerByClientId && playerByClientId.AdminLevel.Value >= 2 && playerByClientId.IsCharacterFullySpawned)
			{
				for (int i = 0; i < 10; i++)
				{
					NetworkBehaviourSingleton<PuckManager>.Instance.Server_SpawnPuck(playerByClientId.Stick.BladeHandlePosition, playerByClientId.Stick.transform.rotation, Vector3.zero);
				}
			}
			break;
		}
	}
}
