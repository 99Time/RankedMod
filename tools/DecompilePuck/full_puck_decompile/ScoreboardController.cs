using System.Collections.Generic;
using UnityEngine;

public class ScoreboardController : MonoBehaviour
{
	private Scoreboard scoreboard;

	private void Awake()
	{
		scoreboard = GetComponent<Scoreboard>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGameStateChanged", Event_OnGameStateChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
		scoreboard.TurnOff();
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGameStateChanged", Event_OnGameStateChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGamePhaseChanged", Event_OnGamePhaseChanged);
	}

	private void Event_OnGamePhaseChanged(Dictionary<string, object> message)
	{
		if ((GamePhase)message["newGamePhase"] == GamePhase.Warmup)
		{
			scoreboard.TurnOff();
		}
		else
		{
			scoreboard.TurnOn();
		}
	}

	private void Event_OnGameStateChanged(Dictionary<string, object> message)
	{
		GameState gameState = (GameState)message["newGameState"];
		switch (gameState.Phase)
		{
		case GamePhase.Playing:
			scoreboard.SetTime(gameState.Time);
			break;
		case GamePhase.FaceOff:
			scoreboard.SetPeriod(gameState.Period);
			scoreboard.SetBlueScore(gameState.BlueScore);
			scoreboard.SetRedScore(gameState.RedScore);
			break;
		}
	}
}
