using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : NetworkBehaviourSingleton<GameManager>
{
	public NetworkVariable<GameState> GameState = new NetworkVariable<GameState>();

	public Dictionary<GamePhase, int> PhaseDurationMap = new Dictionary<GamePhase, int>();

	private IEnumerator gameStateTickCoroutine;

	private IEnumerator debugGameStateCoroutine;

	private int remainingPlayTime;

	public GamePhase Phase => GameState.Value.Phase;

	public bool IsOvertime => GameState.Value.Period > 3;

	public bool IsFirstFaceOff
	{
		get
		{
			if (GameState.Value.Period == 1 && GameState.Value.Phase == GamePhase.FaceOff && GameState.Value.BlueScore == 0)
			{
				return GameState.Value.RedScore == 0;
			}
			return false;
		}
	}

	public bool IsDebugGameStateCoroutineRunning => debugGameStateCoroutine != null;

	public override void OnNetworkSpawn()
	{
		MonoBehaviourSingleton<InputManager>.Instance.DebugGameStateAction.performed += OnDebugGameStateActionPerofrmed;
		MonoBehaviourSingleton<InputManager>.Instance.DebugShootAction.performed += OnDebugShootActionPerformed;
		GameState.Initialize(this);
		if (base.IsServer)
		{
			GameState.Value = default(GameState);
		}
		NetworkVariable<GameState> gameState = GameState;
		gameState.OnValueChanged = (NetworkVariable<GameState>.OnValueChangedDelegate)Delegate.Combine(gameState.OnValueChanged, new NetworkVariable<GameState>.OnValueChangedDelegate(OnGameStateChanged));
		Client_InitializeNetworkVariables();
		base.OnNetworkSpawn();
	}

	protected override void OnNetworkSessionSynchronized()
	{
		Client_InitializeNetworkVariables();
		base.OnNetworkSessionSynchronized();
	}

	public override void OnNetworkDespawn()
	{
		MonoBehaviourSingleton<InputManager>.Instance.DebugGameStateAction.performed -= OnDebugGameStateActionPerofrmed;
		MonoBehaviourSingleton<InputManager>.Instance.DebugShootAction.performed -= OnDebugShootActionPerformed;
		NetworkVariable<GameState> gameState = GameState;
		gameState.OnValueChanged = (NetworkVariable<GameState>.OnValueChangedDelegate)Delegate.Remove(gameState.OnValueChanged, new NetworkVariable<GameState>.OnValueChangedDelegate(OnGameStateChanged));
		GameState.Dispose();
		if (NetworkManager.Singleton.IsServer)
		{
			Server_StopGameStateTickCoroutine();
		}
		base.OnNetworkDespawn();
	}

	private void OnDebugGameStateActionPerofrmed(InputAction.CallbackContext context)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			if (IsDebugGameStateCoroutineRunning)
			{
				Debug.Log("[GameManager] Stopping debug game state coroutine");
				Server_StopDebugGameStateCoroutine();
			}
			else
			{
				Debug.Log("[GameManager] Starting debug game state coroutine");
				Server_StartDebugGameStateCoroutine(0.1f);
			}
		}
	}

	private void OnDebugShootActionPerformed(InputAction.CallbackContext context)
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return;
		}
		Player localPlayer = NetworkBehaviourSingleton<PlayerManager>.Instance.GetLocalPlayer();
		if ((bool)localPlayer)
		{
			PlayerBodyV2 playerBody = localPlayer.PlayerBody;
			if ((bool)playerBody)
			{
				NetworkBehaviourSingleton<PuckManager>.Instance.Server_SpawnPuck(playerBody.transform.position + playerBody.transform.forward * 2.25f + Vector3.up * 0.1f, Quaternion.identity, Vector3.zero);
			}
		}
	}

	private void OnGameStateChanged(GameState oldGameState, GameState newGameState)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnGameStateChanged", new Dictionary<string, object>
		{
			{ "oldGameState", oldGameState },
			{ "newGameState", newGameState }
		});
		if (oldGameState.Phase != newGameState.Phase)
		{
			Debug.Log($"[GameManager] Game phase changed from {oldGameState.Phase} to {newGameState.Phase}");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnGamePhaseChanged", new Dictionary<string, object>
			{
				{ "oldGamePhase", oldGameState.Phase },
				{ "newGamePhase", newGameState.Phase },
				{ "gameState", newGameState },
				{ "period", newGameState.Period },
				{ "time", newGameState.Time },
				{ "isOvertime", IsOvertime },
				{ "isFirstFaceOff", IsFirstFaceOff }
			});
			if (newGameState.Phase == GamePhase.GameOver)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnGameOver", new Dictionary<string, object>
				{
					{
						"winningTeam",
						(GameState.Value.BlueScore > GameState.Value.RedScore) ? PlayerTeam.Blue : PlayerTeam.Red
					},
					{
						"blueScore",
						GameState.Value.BlueScore
					},
					{
						"redScore",
						GameState.Value.RedScore
					}
				});
			}
		}
	}

	public void Server_UpdateGameState(GamePhase? phase = null, int? time = null, int? period = null, int? blueScore = null, int? redScore = null)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			GameState.Value = new GameState
			{
				Phase = ((!phase.HasValue) ? GameState.Value.Phase : phase.Value),
				Time = ((!time.HasValue) ? GameState.Value.Time : time.Value),
				Period = ((!period.HasValue) ? GameState.Value.Period : period.Value),
				BlueScore = ((!blueScore.HasValue) ? GameState.Value.BlueScore : blueScore.Value),
				RedScore = ((!redScore.HasValue) ? GameState.Value.RedScore : redScore.Value)
			};
		}
	}

	public void Server_ResetGameState(bool resetPhase = false)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Server_UpdateGameState((!resetPhase) ? GameState.Value.Phase : GamePhase.None, 0, 0, 0, 0);
		}
	}

	public void Server_SetPhase(GamePhase phase, int time = -1)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			if (phase == GamePhase.Playing)
			{
				Server_UpdateGameState(phase, (time == -1) ? remainingPlayTime : time);
			}
			else
			{
				Server_UpdateGameState(phase, (time == -1) ? PhaseDurationMap[phase] : time);
			}
		}
	}

	public void Server_StartGame(bool warmup = true, int warmupTime = -1)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Server_ResetGameState();
			int? period = 1;
			int? blueScore = 0;
			int? redScore = 0;
			Server_UpdateGameState(null, null, period, blueScore, redScore);
			if (warmup)
			{
				Server_SetPhase(GamePhase.Warmup, warmupTime);
			}
			else
			{
				Server_SetPhase(GamePhase.FaceOff);
			}
			remainingPlayTime = PhaseDurationMap[GamePhase.Playing];
		}
	}

	public void Server_GameOver()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Server_SetPhase(GamePhase.GameOver);
		}
	}

	public void Server_Pause()
	{
		Server_StopGameStateTickCoroutine();
	}

	public void Server_Resume()
	{
		Server_StartGameStateTickCoroutine();
	}

	public void Server_StartGameStateTickCoroutine()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Server_StopGameStateTickCoroutine();
			gameStateTickCoroutine = Server_IGameStateTick();
			StartCoroutine(gameStateTickCoroutine);
		}
	}

	public void Server_StopGameStateTickCoroutine()
	{
		if (NetworkManager.Singleton.IsServer && gameStateTickCoroutine != null)
		{
			StopCoroutine(gameStateTickCoroutine);
		}
	}

	private IEnumerator Server_IGameStateTick()
	{
		yield return new WaitForSeconds(1f);
		Server_OnGameStateTick();
		Server_StartGameStateTickCoroutine();
	}

	public void Server_StartDebugGameStateCoroutine(float delay)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			Server_StopDebugGameStateCoroutine();
			debugGameStateCoroutine = Server_IDebugGameState(delay);
			StartCoroutine(debugGameStateCoroutine);
		}
	}

	public void Server_StopDebugGameStateCoroutine()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			if (debugGameStateCoroutine != null)
			{
				StopCoroutine(debugGameStateCoroutine);
			}
			debugGameStateCoroutine = null;
		}
	}

	private IEnumerator Server_IDebugGameState(float delay)
	{
		Server_SetPhase(GamePhase.FaceOff);
		yield return new WaitForSeconds(delay);
		Server_SetPhase(GamePhase.Warmup);
		debugGameStateCoroutine = Server_IDebugGameState(delay);
		StartCoroutine(debugGameStateCoroutine);
	}

	private void Server_OnGameStateTick()
	{
		if (!NetworkManager.Singleton.IsServer)
		{
			return;
		}
		int? period;
		if (GameState.Value.Time <= 0)
		{
			switch (GameState.Value.Phase)
			{
			case GamePhase.Warmup:
				Server_SetPhase(GamePhase.FaceOff);
				break;
			case GamePhase.FaceOff:
				Server_SetPhase(GamePhase.Playing);
				break;
			case GamePhase.Playing:
				if (GameState.Value.Period < 3)
				{
					Server_SetPhase(GamePhase.PeriodOver);
					period = GameState.Value.Period + 1;
					Server_UpdateGameState(null, null, period);
				}
				else if (GameState.Value.BlueScore == GameState.Value.RedScore)
				{
					Server_SetPhase(GamePhase.PeriodOver);
					period = GameState.Value.Period + 1;
					Server_UpdateGameState(null, null, period);
				}
				else
				{
					Server_GameOver();
				}
				break;
			case GamePhase.BlueScore:
			case GamePhase.RedScore:
				Server_SetPhase(GamePhase.Replay);
				break;
			case GamePhase.Replay:
				if (IsOvertime)
				{
					Server_GameOver();
				}
				else
				{
					Server_SetPhase(GamePhase.FaceOff);
				}
				break;
			case GamePhase.PeriodOver:
				remainingPlayTime = PhaseDurationMap[GamePhase.Playing];
				Server_SetPhase(GamePhase.FaceOff);
				break;
			case GamePhase.GameOver:
				Server_StartGame(warmup: true, NetworkBehaviourSingleton<PlayerManager>.Instance.IsEnoughPlayersForPlaying() ? 60 : (-1));
				break;
			}
		}
		period = GameState.Value.Time - 1;
		Server_UpdateGameState(null, period);
	}

	public void Server_GoalScored(PlayerTeam team, Player lastPlayer, Player goalPlayer, Player assistPlayer, Player secondAssistPlayer, Puck puck)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			remainingPlayTime = GameState.Value.Time;
			switch (team)
			{
			case PlayerTeam.Blue:
			{
				int? redScore = GameState.Value.BlueScore + 1;
				Server_UpdateGameState(null, null, null, redScore);
				Server_SetPhase(GamePhase.BlueScore);
				break;
			}
			case PlayerTeam.Red:
			{
				int? redScore = GameState.Value.RedScore + 1;
				Server_UpdateGameState(null, null, null, null, redScore);
				Server_SetPhase(GamePhase.RedScore);
				break;
			}
			}
			Server_GoalScoredRpc(team, lastPlayer != null, lastPlayer ? lastPlayer.OwnerClientId : 0, goalPlayer != null, goalPlayer ? goalPlayer.OwnerClientId : 0, assistPlayer != null, assistPlayer ? assistPlayer.OwnerClientId : 0, secondAssistPlayer != null, secondAssistPlayer ? secondAssistPlayer.OwnerClientId : 0, puck.Speed, puck.ShotSpeed);
			Debug.Log($"[GameManager] Goal scored by {team} ({GameState.Value.BlueScore}-{GameState.Value.RedScore})");
			Debug.Log($"[GameManager] Goal player: {goalPlayer?.Username.Value} ({goalPlayer?.OwnerClientId})");
			Debug.Log($"[GameManager] Assist player: {assistPlayer?.Username.Value} ({assistPlayer?.OwnerClientId})");
			Debug.Log($"[GameManager] Second assist player: {secondAssistPlayer?.Username.Value} ({secondAssistPlayer?.OwnerClientId})");
		}
	}

	[Rpc(SendTo.Everyone)]
	public void Server_GoalScoredRpc(PlayerTeam team, bool hasLastPlayer, ulong lastPlayerClientId, bool hasGoalPlayer, ulong goalPlayerClientId, bool hasAssistPlayer, ulong assistPlayerClientId, bool hasSecondAssistPlayer, ulong secondAssistPlayerClientId, float speedAcrossLine, float highestSpeedSinceStick, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
				FastBufferWriter bufferWriter = __beginSendRpc(460221987u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in team, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in hasLastPlayer, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, lastPlayerClientId);
				bufferWriter.WriteValueSafe(in hasGoalPlayer, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, goalPlayerClientId);
				bufferWriter.WriteValueSafe(in hasAssistPlayer, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, assistPlayerClientId);
				bufferWriter.WriteValueSafe(in hasSecondAssistPlayer, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, secondAssistPlayerClientId);
				bufferWriter.WriteValueSafe(in speedAcrossLine, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in highestSpeedSinceStick, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 460221987u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_OnGoalScored", new Dictionary<string, object>
				{
					{ "team", team },
					{ "hasLastPlayer", hasLastPlayer },
					{ "lastPlayerClientId", lastPlayerClientId },
					{ "hasGoalPlayer", hasGoalPlayer },
					{ "goalPlayerClientId", goalPlayerClientId },
					{ "hasAssistPlayer", hasAssistPlayer },
					{ "assistPlayerClientId", assistPlayerClientId },
					{ "hasSecondAssistPlayer", hasSecondAssistPlayer },
					{ "secondAssistPlayerClientId", secondAssistPlayerClientId },
					{ "speedAcrossLine", speedAcrossLine },
					{ "highestSpeedSinceStick", highestSpeedSinceStick }
				});
			}
		}
	}

	public void Client_InitializeNetworkVariables()
	{
		OnGameStateChanged(GameState.Value, GameState.Value);
	}

	protected override void __initializeVariables()
	{
		if (GameState == null)
		{
			throw new Exception("GameManager.GameState cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		GameState.Initialize(this);
		__nameNetworkVariable(GameState, "GameState");
		NetworkVariableFields.Add(GameState);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(460221987u, __rpc_handler_460221987, "Server_GoalScoredRpc");
		base.__initializeRpcs();
	}

	private static void __rpc_handler_460221987(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out PlayerTeam value, default(FastBufferWriter.ForEnums));
			reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
			reader.ReadValueSafe(out bool value4, default(FastBufferWriter.ForPrimitives));
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value5);
			reader.ReadValueSafe(out bool value6, default(FastBufferWriter.ForPrimitives));
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value7);
			reader.ReadValueSafe(out bool value8, default(FastBufferWriter.ForPrimitives));
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value9);
			reader.ReadValueSafe(out float value10, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out float value11, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			target.__rpc_exec_stage = __RpcExecStage.Execute;
			((GameManager)target).Server_GoalScoredRpc(value, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, ext);
			target.__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected internal override string __getTypeName()
	{
		return "GameManager";
	}
}
