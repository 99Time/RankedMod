using System.Collections.Generic;
using Unity.Netcode;

public class UIGameStateController : NetworkBehaviour
{
	private UIGameState uiGameState;

	private void Awake()
	{
		uiGameState = GetComponent<UIGameState>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGameStateChanged", Event_OnGameStateChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnShowGameUserInterfaceChanged", Event_Client_OnShowGameUserInterfaceChanged);
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGameStateChanged", Event_OnGameStateChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnShowGameUserInterfaceChanged", Event_Client_OnShowGameUserInterfaceChanged);
		base.OnDestroy();
	}

	private void Event_OnGameStateChanged(Dictionary<string, object> message)
	{
		GameState gameState = (GameState)message["newGameState"];
		switch (gameState.Phase)
		{
		case GamePhase.Warmup:
			uiGameState.SetGameTime(gameState.Time);
			uiGameState.SetGamePhase("WARMUP");
			break;
		case GamePhase.FaceOff:
			uiGameState.SetGameTime(gameState.Time);
			uiGameState.SetGamePhase("FACE OFF");
			break;
		case GamePhase.BlueScore:
		case GamePhase.RedScore:
			uiGameState.SetGameTime(gameState.Time);
			uiGameState.SetGamePhase("GOAL");
			break;
		case GamePhase.Replay:
			uiGameState.SetGameTime(gameState.Time);
			uiGameState.SetGamePhase("REPLAY");
			break;
		case GamePhase.PeriodOver:
			uiGameState.SetGameTime(gameState.Time);
			uiGameState.SetGamePhase("INTERMISSION");
			break;
		case GamePhase.GameOver:
			uiGameState.SetGameTime(gameState.Time);
			uiGameState.SetGamePhase("GAME OVER");
			break;
		default:
			uiGameState.SetGameTime(gameState.Time);
			if (gameState.Period <= 3)
			{
				uiGameState.SetGamePhase($"PERIOD {gameState.Period}");
			}
			else
			{
				uiGameState.SetGamePhase("OVERTIME");
			}
			break;
		}
		uiGameState.SetBlueTeamScore(gameState.BlueScore);
		uiGameState.SetRedTeamScore(gameState.RedScore);
	}

	private void Event_Client_OnShowGameUserInterfaceChanged(Dictionary<string, object> message)
	{
		if (NetworkBehaviourSingleton<UIManager>.Instance.UIState != UIState.MainMenu)
		{
			if ((bool)message["value"])
			{
				uiGameState.Show();
			}
			else
			{
				uiGameState.Hide();
			}
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
		return "UIGameStateController";
	}
}
