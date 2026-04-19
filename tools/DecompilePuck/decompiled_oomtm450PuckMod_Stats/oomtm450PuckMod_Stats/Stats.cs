using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Codebase;
using HarmonyLib;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using oomtm450PuckMod_Stats.Configs;

namespace oomtm450PuckMod_Stats;

public class Stats : IPuckMod
{
	[HarmonyPatch(typeof(PuckManager), "Server_SpawnPuck")]
	public class PuckManager_Server_SpawnPuck_Patch
	{
		[HarmonyPostfix]
		public static void Postfix(ref Puck __result, Vector3 position, Quaternion rotation, Vector3 velocity, bool isReplay)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Invalid comparison between Unknown and I4
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Invalid comparison between Unknown and I4
			try
			{
				if (!(!ServerFunc.IsDedicatedServer() || isReplay) && ((int)NetworkBehaviourSingleton<GameManager>.Instance.Phase == 3 || (int)NetworkBehaviourSingleton<GameManager>.Instance.Phase == 2))
				{
					((Component)__result).gameObject.AddComponent<PuckRaycast>();
					_puckRaycast = ((Component)__result).gameObject.GetComponent<PuckRaycast>();
				}
			}
			catch (Exception arg)
			{
				Logging.LogError($"Error in PuckManager_Server_SpawnPuck_Patch Postfix().\n{arg}", ServerConfig);
			}
		}
	}

	[HarmonyPatch(typeof(GameManager), "Server_GoalScored")]
	public class GameManager_Server_GoalScored_Patch
	{
		[HarmonyPrefix]
		public static bool Prefix(PlayerTeam team, ref Player lastPlayer, ref Player goalPlayer, ref Player assistPlayer, ref Player secondAssistPlayer, Puck puck)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0200: Unknown result type (might be due to invalid IL or missing references)
			//IL_0250: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0106: Unknown result type (might be due to invalid IL or missing references)
			//IL_010b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0137: Unknown result type (might be due to invalid IL or missing references)
			//IL_013c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0152: Unknown result type (might be due to invalid IL or missing references)
			//IL_0157: Unknown result type (might be due to invalid IL or missing references)
			//IL_0175: Unknown result type (might be due to invalid IL or missing references)
			//IL_017a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0190: Unknown result type (might be due to invalid IL or missing references)
			//IL_0195: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (!ServerFunc.IsDedicatedServer() || RulesetModEnabled() || !_logic)
				{
					return true;
				}
				if ((Object)(object)goalPlayer != (Object)null)
				{
					Player val = (from x in NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers(false)
						where ((object)x.SteamId.Value/*cast due to .constrained prefix*/).ToString() == _lastPlayerOnPuckTipIncludedSteamId[team].SteamId
						select x).FirstOrDefault();
					if ((Object)(object)val != (Object)null && ((object)val.SteamId.Value/*cast due to .constrained prefix*/).ToString() != ((object)goalPlayer.SteamId.Value/*cast due to .constrained prefix*/).ToString())
					{
						secondAssistPlayer = assistPlayer;
						assistPlayer = goalPlayer;
						goalPlayer = (from x in NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers(false)
							where ((object)x.SteamId.Value/*cast due to .constrained prefix*/).ToString() == _lastPlayerOnPuckTipIncludedSteamId[team].SteamId
							select x).FirstOrDefault();
						while ((Object)(object)assistPlayer != (Object)null && ((object)assistPlayer.SteamId.Value/*cast due to .constrained prefix*/).ToString() == ((object)goalPlayer.SteamId.Value/*cast due to .constrained prefix*/).ToString())
						{
							assistPlayer = secondAssistPlayer;
							secondAssistPlayer = null;
						}
						if ((Object)(object)secondAssistPlayer != (Object)null && (((object)secondAssistPlayer.SteamId.Value/*cast due to .constrained prefix*/).ToString() == ((object)assistPlayer.SteamId.Value/*cast due to .constrained prefix*/).ToString() || ((object)secondAssistPlayer.SteamId.Value/*cast due to .constrained prefix*/).ToString() == ((object)goalPlayer.SteamId.Value/*cast due to .constrained prefix*/).ToString()))
						{
							secondAssistPlayer = null;
						}
					}
					SendSavePercDuringGoal(team, SendSOGDuringGoal(goalPlayer));
					return true;
				}
				NetworkBehaviourSingleton<UIChat>.Instance.Server_SendSystemChatMessage($"OWN GOAL BY {NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(FixedString32Bytes.op_Implicit(_lastPlayerOnPuckTipIncludedSteamId[TeamFunc.GetOtherTeam(team)].SteamId)).Username.Value}");
				goalPlayer = (from x in NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers(false)
					where ((object)x.SteamId.Value/*cast due to .constrained prefix*/).ToString() == _lastPlayerOnPuckTipIncludedSteamId[team].SteamId
					select x).FirstOrDefault();
				bool saveWasCounted = false;
				if ((Object)(object)goalPlayer != (Object)null)
				{
					lastPlayer = goalPlayer;
					saveWasCounted = SendSOGDuringGoal(goalPlayer);
				}
				SendSavePercDuringGoal(team, saveWasCounted);
			}
			catch (Exception arg)
			{
				Logging.LogError(string.Format("Error in {0} Prefix().\n{1}", "GameManager_Server_GoalScored_Patch", arg), ServerConfig);
			}
			return true;
		}

		[HarmonyPostfix]
		public static void Postfix(PlayerTeam team, Player lastPlayer, Player goalPlayer, Player assistPlayer, Player secondAssistPlayer, Puck puck)
		{
			//IL_0127: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Invalid comparison between Unknown and I4
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_0155: Unknown result type (might be due to invalid IL or missing references)
			//IL_015a: Unknown result type (might be due to invalid IL or missing references)
			//IL_017e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0183: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (!ServerFunc.IsDedicatedServer())
				{
					return;
				}
				foreach (Player player in NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers(false))
				{
					if (PlayerFunc.IsPlayerPlaying(player) && !PlayerFunc.IsGoalie(player))
					{
						string text = ((object)player.SteamId.Value/*cast due to .constrained prefix*/).ToString();
						if (!_plusMinus.TryGetValue(text, out var value))
						{
							_plusMinus.Add(text, 0);
						}
						if (player.Team.Value == team)
						{
							_plusMinus[text]++;
						}
						else
						{
							_plusMinus[text]--;
						}
						string dataName = "oomtm450_statsPLUSMINUS" + text;
						value = _plusMinus[text];
						NetworkCommunication.SendDataToAll(dataName, value.ToString(), "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
						LogPlusMinus(text, _plusMinus[text]);
					}
				}
				LockList<string> lockList;
				LockList<string> lockList2;
				if ((int)team == 2)
				{
					lockList = _blueGoals;
					lockList2 = _blueAssists;
				}
				else
				{
					lockList = _redGoals;
					lockList2 = _redAssists;
				}
				if ((Object)(object)goalPlayer != (Object)null)
				{
					lockList.Add(((object)goalPlayer.SteamId.Value/*cast due to .constrained prefix*/).ToString());
				}
				if ((Object)(object)assistPlayer != (Object)null)
				{
					lockList2.Add(((object)assistPlayer.SteamId.Value/*cast due to .constrained prefix*/).ToString());
				}
				if ((Object)(object)secondAssistPlayer != (Object)null)
				{
					lockList2.Add(((object)secondAssistPlayer.SteamId.Value/*cast due to .constrained prefix*/).ToString());
				}
			}
			catch (Exception arg)
			{
				Logging.LogError(string.Format("Error in {0} Postfix().\n{1}", "GameManager_Server_GoalScored_Patch", arg), ServerConfig);
			}
		}
	}

	[HarmonyPatch(typeof(UIScoreboard), "RemovePlayer")]
	public class UIScoreboard_RemovePlayer_Patch
	{
		[HarmonyPostfix]
		public static void Postfix(Player player)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (!ServerFunc.IsDedicatedServer())
				{
					_sogLabels.Remove(((object)player.SteamId.Value/*cast due to .constrained prefix*/).ToString());
					_hasUpdatedUIScoreboard.Remove(((object)player.SteamId.Value/*cast due to .constrained prefix*/).ToString());
				}
			}
			catch (Exception arg)
			{
				Logging.LogError(string.Format("Error in {0} Postfix().\n{1}", "UIScoreboard_RemovePlayer_Patch", arg), _clientConfig);
			}
		}
	}

	[HarmonyPatch(typeof(GameManager), "Server_ResetGameState")]
	public class GameManager_Server_ResetGameState_Patch
	{
		[HarmonyPostfix]
		public static void Postfix(bool resetPhase)
		{
			try
			{
				if (!ServerFunc.IsDedicatedServer())
				{
					return;
				}
				List<Player> players = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers(false);
				foreach (string key in new List<string>(_savePerc.Keys))
				{
					if ((Object)(object)players.FirstOrDefault((Player x) => ((object)x.SteamId.Value/*cast due to .constrained prefix*/).ToString() == key) != (Object)null)
					{
						_savePerc[key] = (0, 0);
					}
					else
					{
						_savePerc.Remove(key);
					}
				}
				foreach (string key2 in new List<string>(_sog.Keys))
				{
					if ((Object)(object)players.FirstOrDefault((Player x) => ((object)x.SteamId.Value/*cast due to .constrained prefix*/).ToString() == key2) != (Object)null)
					{
						_sog[key2] = 0;
					}
					else
					{
						_sog.Remove(key2);
					}
				}
				foreach (string key3 in new List<string>(_stickSaves.Keys))
				{
					if ((Object)(object)players.FirstOrDefault((Player x) => ((object)x.SteamId.Value/*cast due to .constrained prefix*/).ToString() == key3) != (Object)null)
					{
						_stickSaves[key3] = 0;
					}
					else
					{
						_stickSaves.Remove(key3);
					}
				}
				foreach (string key4 in new List<string>(_blocks.Keys))
				{
					if ((Object)(object)players.FirstOrDefault((Player x) => ((object)x.SteamId.Value/*cast due to .constrained prefix*/).ToString() == key4) != (Object)null)
					{
						_blocks[key4] = 0;
					}
					else
					{
						_blocks.Remove(key4);
					}
				}
				foreach (string key5 in new List<string>(_hits.Keys))
				{
					if ((Object)(object)players.FirstOrDefault((Player x) => ((object)x.SteamId.Value/*cast due to .constrained prefix*/).ToString() == key5) != (Object)null)
					{
						_hits[key5] = 0;
					}
					else
					{
						_hits.Remove(key5);
					}
				}
				foreach (string key6 in new List<string>(_takeaways.Keys))
				{
					if ((Object)(object)players.FirstOrDefault((Player x) => ((object)x.SteamId.Value/*cast due to .constrained prefix*/).ToString() == key6) != (Object)null)
					{
						_takeaways[key6] = 0;
					}
					else
					{
						_takeaways.Remove(key6);
					}
				}
				foreach (string key7 in new List<string>(_turnovers.Keys))
				{
					if ((Object)(object)players.FirstOrDefault((Player x) => ((object)x.SteamId.Value/*cast due to .constrained prefix*/).ToString() == key7) != (Object)null)
					{
						_turnovers[key7] = 0;
					}
					else
					{
						_turnovers.Remove(key7);
					}
				}
				foreach (string key8 in new List<string>(_passes.Keys))
				{
					if ((Object)(object)players.FirstOrDefault((Player x) => ((object)x.SteamId.Value/*cast due to .constrained prefix*/).ToString() == key8) != (Object)null)
					{
						_passes[key8] = 0;
					}
					else
					{
						_passes.Remove(key8);
					}
				}
				foreach (string key9 in new List<string>(_plusMinus.Keys))
				{
					if ((Object)(object)players.FirstOrDefault((Player x) => ((object)x.SteamId.Value/*cast due to .constrained prefix*/).ToString() == key9) != (Object)null)
					{
						_plusMinus[key9] = 0;
					}
					else
					{
						_plusMinus.Remove(key9);
					}
				}
				_blueGoals.Clear();
				_blueAssists.Clear();
				_redGoals.Clear();
				_redAssists.Clear();
				_lastPossession = new Possession();
				NetworkCommunication.SendDataToAll("oomtm450_statsRESETALL", "1", "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
				_sentOutOfDateMessage.Clear();
			}
			catch (Exception arg)
			{
				Logging.LogError(string.Format("Error in {0} Postfix().\n{1}", "GameManager_Server_ResetGameState_Patch", arg), ServerConfig);
			}
		}
	}

	[HarmonyPatch(typeof(ServerManager), "Update")]
	public class ServerManager_Update_Patch
	{
		[HarmonyPostfix]
		public static void Postfix()
		{
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Invalid comparison between Unknown and I4
			//IL_04a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_04e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_050b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0515: Unknown result type (might be due to invalid IL or missing references)
			//IL_051a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0546: Unknown result type (might be due to invalid IL or missing references)
			//IL_0563: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_05d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_056f: Unknown result type (might be due to invalid IL or missing references)
			//IL_057b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0339: Unknown result type (might be due to invalid IL or missing references)
			//IL_019f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_037d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0382: Unknown result type (might be due to invalid IL or missing references)
			//IL_0389: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_03bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_03cb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0456: Unknown result type (might be due to invalid IL or missing references)
			//IL_03ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_03f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0407: Unknown result type (might be due to invalid IL or missing references)
			//IL_0418: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (!ServerFunc.IsDedicatedServer() || !_logic)
				{
					return;
				}
				bool sendSavePercDuringGoalNextFrame = _sendSavePercDuringGoalNextFrame;
				if (sendSavePercDuringGoalNextFrame)
				{
					_sendSavePercDuringGoalNextFrame = false;
					SendSavePercDuringGoal(_sendSavePercDuringGoalNextFrame_Player.Team.Value, SendSOGDuringGoal(_sendSavePercDuringGoalNextFrame_Player));
				}
				if ((Object)(object)NetworkBehaviourSingleton<PlayerManager>.Instance == (Object)null || (Object)(object)NetworkBehaviourSingleton<PuckManager>.Instance == (Object)null || (int)NetworkBehaviourSingleton<GameManager>.Instance.Phase != 3 || _paused)
				{
					return;
				}
				if (!sendSavePercDuringGoalNextFrame)
				{
					foreach (PlayerTeam item2 in new List<PlayerTeam>(_checkIfPuckWasSaved.Keys))
					{
						SaveCheck saveCheck = _checkIfPuckWasSaved[item2];
						int value;
						if (!saveCheck.HasToCheck)
						{
							_checkIfPuckWasSaved[item2] = new SaveCheck();
						}
						else if (!_puckRaycast.PuckIsGoingToNet[item2] && !_lastShotWasCounted[saveCheck.ShooterTeam])
						{
							if (!_sog.TryGetValue(saveCheck.ShooterSteamId, out value))
							{
								_sog.Add(saveCheck.ShooterSteamId, 0);
							}
							_sog[saveCheck.ShooterSteamId]++;
							string dataName = "oomtm450_statsSOG" + saveCheck.ShooterSteamId;
							value = _sog[saveCheck.ShooterSteamId];
							NetworkCommunication.SendDataToAll(dataName, value.ToString(), "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
							LogSOG(saveCheck.ShooterSteamId, _sog[saveCheck.ShooterSteamId]);
							_lastShotWasCounted[saveCheck.ShooterTeam] = true;
							Player otherTeamGoalie = PlayerFunc.GetOtherTeamGoalie(saveCheck.ShooterTeam);
							if ((Object)(object)otherTeamGoalie != (Object)null)
							{
								string text = ((object)otherTeamGoalie.SteamId.Value/*cast due to .constrained prefix*/).ToString();
								if (!_savePerc.TryGetValue(text, out (int, int) value2))
								{
									_savePerc.Add(text, (0, 0));
									value2 = (0, 0);
								}
								LockDictionary<string, (int Saves, int Shots)> savePerc = _savePerc;
								int item = ++value2.Item1;
								value = ++value2.Item2;
								var (saves, sog) = (savePerc[text] = (item, value));
								NetworkCommunication.SendDataToAll("oomtm450_statsSAVEPERC" + text, _savePerc[text].ToString(), "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
								LogSavePerc(text, saves, sog);
								if (saveCheck.HitStick)
								{
									if (!_stickSaves.TryGetValue(text, out var value3))
									{
										_stickSaves.Add(text, 0);
										value3 = 0;
									}
									value = (_stickSaves[text] = ++value3);
									int stickSaves = value;
									LogStickSave(text, stickSaves);
								}
							}
							_checkIfPuckWasSaved[item2] = new SaveCheck();
							_checkIfPuckWasBlocked[item2] = new BlockCheck();
						}
						else
						{
							value = ++saveCheck.FramesChecked;
							if (value > NetworkBehaviourSingleton<ServerManager>.Instance.ServerConfigurationManager.ServerConfiguration.serverTickRate)
							{
								_checkIfPuckWasSaved[item2] = new SaveCheck();
							}
						}
					}
					foreach (PlayerTeam item3 in new List<PlayerTeam>(_checkIfPuckWasBlocked.Keys))
					{
						BlockCheck blockCheck = _checkIfPuckWasBlocked[item3];
						if (!blockCheck.HasToCheck)
						{
							_checkIfPuckWasBlocked[item3] = new BlockCheck();
						}
						else if (!_puckRaycast.PuckIsGoingToNet[item3] && !_lastBlockWasCounted[blockCheck.ShooterTeam])
						{
							ProcessBlock(blockCheck.BlockerSteamId);
							_lastBlockWasCounted[blockCheck.ShooterTeam] = true;
							PlayerFunc.GetOtherTeamGoalie(blockCheck.ShooterTeam);
							_checkIfPuckWasSaved[item3] = new SaveCheck();
							_checkIfPuckWasBlocked[item3] = new BlockCheck();
						}
						else if (++blockCheck.FramesChecked > NetworkBehaviourSingleton<ServerManager>.Instance.ServerConfigurationManager.ServerConfiguration.serverTickRate)
						{
							_checkIfPuckWasBlocked[item3] = new BlockCheck();
						}
					}
				}
				Puck puck = NetworkBehaviourSingleton<PuckManager>.Instance.GetPuck(false);
				if (Object.op_Implicit((Object)(object)puck))
				{
					_puckZCoordinateDifference = (((Component)puck.Rigidbody).transform.position.z - _puckLastCoordinate.z) / 240f * (float)NetworkBehaviourSingleton<ServerManager>.Instance.ServerConfigurationManager.ServerConfiguration.serverTickRate;
					_puckLastCoordinate = new Vector3(((Component)puck.Rigidbody).transform.position.x, ((Component)puck.Rigidbody).transform.position.y, ((Component)puck.Rigidbody).transform.position.z);
				}
				string playerSteamIdInPossession = PlayerFunc.GetPlayerSteamIdInPossession(ServerConfig.MinPossessionMilliseconds, _playersCurrentPuckTouch);
				if (string.IsNullOrEmpty(playerSteamIdInPossession))
				{
					return;
				}
				Player playerBySteamId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(FixedString32Bytes.op_Implicit(playerSteamIdInPossession));
				if (PlayerFunc.IsPlayerPlaying(playerBySteamId))
				{
					if ((int)_lastPossession.Team != 0 && _lastPossession.Team != playerBySteamId.Team.Value && (DateTime.UtcNow - _lastPossession.Date).TotalMilliseconds < (double)ServerConfig.TurnoverThresholdMilliseconds)
					{
						ProcessTakeaways(playerSteamIdInPossession);
						ProcessTurnovers(_lastPossession.SteamId);
					}
					_lastPossession = new Possession
					{
						SteamId = playerSteamIdInPossession,
						Team = playerBySteamId.Team.Value,
						Date = DateTime.UtcNow
					};
				}
				else
				{
					_lastPossession = new Possession();
				}
			}
			catch (Exception arg)
			{
				Logging.LogError($"Error in ServerManager_Update_Patch Postfix().\n{arg}", ServerConfig);
			}
		}
	}

	[HarmonyPatch(typeof(UIScoreboard), "UpdatePlayer")]
	public class UIScoreboard_UpdatePlayer_Patch
	{
		[HarmonyPostfix]
		public static void Postfix(UIScoreboard __instance, Player player)
		{
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Expected O, but got Unknown
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_014b: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (ServerFunc.IsDedicatedServer())
				{
					return;
				}
				if (!_hasRegisteredWithNamedMessageHandler || !_serverHasResponded)
				{
					NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("oomtm450_stats_server", new HandleNamedMessageDelegate(ReceiveData));
					_hasRegisteredWithNamedMessageHandler = true;
					DateTime utcNow = DateTime.UtcNow;
					if (_lastDateTimeAskStartupData + TimeSpan.FromSeconds(1.0) < utcNow && _askServerForStartupDataCount++ < 10)
					{
						_lastDateTimeAskStartupData = utcNow;
						NetworkCommunication.SendData("oomtm450_statsASKDATA", "1", 0uL, "oomtm450_stats_client", _clientConfig, (NetworkDelivery)4);
					}
				}
				else if (_askForKick)
				{
					_askForKick = false;
					NetworkCommunication.SendData("oomtm450_stats_kick", "1", 0uL, "oomtm450_stats_client", _clientConfig, (NetworkDelivery)4);
				}
				else if (_addServerModVersionOutOfDateMessage)
				{
					_addServerModVersionOutOfDateMessage = false;
					NetworkBehaviourSingleton<UIChat>.Instance.AddChatMessage("Server's Stats mod is out of date. Some functionalities might not work properly.");
				}
				ScoreboardModifications(enable: true);
				string text = ((object)player.SteamId.Value/*cast due to .constrained prefix*/).ToString();
				if (!string.IsNullOrEmpty(text) && _stars.Values.Contains(text))
				{
					Dictionary<Player, VisualElement> privateField = SystemFunc.GetPrivateField<Dictionary<Player, VisualElement>>(typeof(UIScoreboard), __instance, "playerVisualElementMap");
					if (privateField.ContainsKey(player))
					{
						Label val = UQueryBuilder<Label>.op_Implicit(UQueryExtensions.Query<Label>(privateField[player], "UsernameLabel", (string)null));
						((TextElement)val).text = GetStarTag(text) + ((TextElement)val).text;
					}
				}
			}
			catch (Exception arg)
			{
				Logging.LogError($"Error in UIScoreboard_UpdateServer_Patch Postfix().\n{arg}", _clientConfig);
			}
		}
	}

	[HarmonyPatch(typeof(Puck), "OnCollisionEnter")]
	public class Puck_OnCollisionEnter_Patch
	{
		[HarmonyPostfix]
		public static void Postfix(Puck __instance, Collision collision)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Invalid comparison between Unknown and I4
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_0182: Unknown result type (might be due to invalid IL or missing references)
			//IL_0187: Unknown result type (might be due to invalid IL or missing references)
			//IL_018c: Unknown result type (might be due to invalid IL or missing references)
			//IL_019d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00db: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0246: Unknown result type (might be due to invalid IL or missing references)
			//IL_0247: Unknown result type (might be due to invalid IL or missing references)
			//IL_024e: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_026f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0287: Unknown result type (might be due to invalid IL or missing references)
			//IL_028c: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0302: Invalid comparison between Unknown and I4
			//IL_020f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0229: Unknown result type (might be due to invalid IL or missing references)
			//IL_031b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0321: Invalid comparison between Unknown and I4
			//IL_036d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0373: Invalid comparison between Unknown and I4
			//IL_03f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_041c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0446: Unknown result type (might be due to invalid IL or missing references)
			//IL_0497: Unknown result type (might be due to invalid IL or missing references)
			//IL_049c: Unknown result type (might be due to invalid IL or missing references)
			//IL_04a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_04a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0470: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_04e4: Unknown result type (might be due to invalid IL or missing references)
			if (!ServerFunc.IsDedicatedServer() || _paused || (int)NetworkBehaviourSingleton<GameManager>.Instance.Phase != 3 || !_logic)
			{
				return;
			}
			try
			{
				Player val = null;
				Stick stick = SystemFunc.GetStick(collision.gameObject);
				if (!Object.op_Implicit((Object)(object)stick))
				{
					PlayerBodyV2 playerBodyV = SystemFunc.GetPlayerBodyV2(collision.gameObject);
					if (!Object.op_Implicit((Object)(object)playerBodyV) || !Object.op_Implicit((Object)(object)playerBodyV.Player))
					{
						return;
					}
					val = playerBodyV.Player;
				}
				else
				{
					if (!Object.op_Implicit((Object)(object)stick.Player))
					{
						return;
					}
					val = stick.Player;
				}
				string text = ((object)val.SteamId.Value/*cast due to .constrained prefix*/).ToString();
				if (!PlayerFunc.IsGoalie(val))
				{
					if (!_playersCurrentPuckTouch.TryGetValue(text, out var value))
					{
						value = new Stopwatch();
						value.Start();
						_playersCurrentPuckTouch.Add(text, value);
					}
					string item = _lastPlayerOnPuckTipIncludedSteamId[_lastTeamOnPuckTipIncluded].SteamId;
					if (!_lastTimeOnCollisionStayOrExitWasCalled.TryGetValue(text, out var value2))
					{
						value2 = new Stopwatch();
						value2.Start();
						_lastTimeOnCollisionStayOrExitWasCalled.Add(text, value2);
					}
					else if (value2.ElapsedMilliseconds > ServerConfig.MaxPossessionMilliseconds || (!string.IsNullOrEmpty(item) && item != text))
					{
						value.Restart();
						if (!string.IsNullOrEmpty(item) && item != text && _playersCurrentPuckTouch.TryGetValue(item, out var value3))
						{
							value3.Reset();
						}
					}
				}
				else
				{
					_lastPossession = new Possession();
				}
				PlayerTeam otherTeam = TeamFunc.GetOtherTeam(val.Team.Value);
				if (_puckRaycast.PuckIsGoingToNet[val.Team.Value])
				{
					if (PlayerFunc.IsGoalie(val) && (double)Math.Abs(((Component)val.PlayerBody.Rigidbody).transform.position.z) > 13.5)
					{
						PlayerTeam val2 = otherTeam;
						string item2 = _lastPlayerOnPuckTipIncludedSteamId[val2].SteamId;
						if (!string.IsNullOrEmpty(item2))
						{
							_checkIfPuckWasSaved[val.Team.Value] = new SaveCheck
							{
								HasToCheck = true,
								ShooterSteamId = item2,
								ShooterTeam = val2,
								HitStick = Object.op_Implicit((Object)(object)stick)
							};
						}
					}
					else
					{
						PlayerTeam val3 = otherTeam;
						if (!string.IsNullOrEmpty(_lastPlayerOnPuckTipIncludedSteamId[val3].SteamId))
						{
							_checkIfPuckWasBlocked[val.Team.Value] = new BlockCheck
							{
								HasToCheck = true,
								BlockerSteamId = ((object)val.SteamId.Value/*cast due to .constrained prefix*/).ToString(),
								ShooterTeam = val3
							};
						}
					}
				}
				else
				{
					if (_lastTeamOnPuckTipIncluded != otherTeam || !PlayerFunc.IsGoalie(val) || !((double)Math.Abs(((Component)val.PlayerBody.Rigidbody).transform.position.z) > 13.5) || (((int)val.Team.Value != 2 || !(_puckZCoordinateDifference > ServerConfig.GoalieSaveCreaseSystemZDelta)) && ((int)val.Team.Value != 3 || !(_puckZCoordinateDifference < 0f - ServerConfig.GoalieSaveCreaseSystemZDelta))))
					{
						return;
					}
					double num = 0.0;
					double num2 = 0.0;
					double num3 = 0.0;
					double num4 = 0.0;
					if ((int)val.Team.Value != 2)
					{
						(num, num2) = ZoneFunc.ICE_X_POSITIONS[IceElement.RedTeam_BluePaint];
						(num3, num4) = ZoneFunc.ICE_Z_POSITIONS[IceElement.RedTeam_BluePaint];
					}
					else
					{
						(num, num2) = ZoneFunc.ICE_X_POSITIONS[IceElement.BlueTeam_BluePaint];
						(num3, num4) = ZoneFunc.ICE_Z_POSITIONS[IceElement.BlueTeam_BluePaint];
					}
					bool flag = true;
					if ((double)(((Component)val.PlayerBody.Rigidbody).transform.position.x - ServerConfig.GoalieRadius) < num || (double)(((Component)val.PlayerBody.Rigidbody).transform.position.x + ServerConfig.GoalieRadius) > num2 || (double)(((Component)val.PlayerBody.Rigidbody).transform.position.z - ServerConfig.GoalieRadius) < num3 || (double)(((Component)val.PlayerBody.Rigidbody).transform.position.z + ServerConfig.GoalieRadius) > num4)
					{
						flag = false;
					}
					if (flag)
					{
						PlayerTeam otherTeam2 = TeamFunc.GetOtherTeam(val.Team.Value);
						string item3 = _lastPlayerOnPuckTipIncludedSteamId[otherTeam2].SteamId;
						if (!string.IsNullOrEmpty(item3))
						{
							_checkIfPuckWasSaved[val.Team.Value] = new SaveCheck
							{
								HasToCheck = true,
								ShooterSteamId = item3,
								ShooterTeam = otherTeam2,
								HitStick = Object.op_Implicit((Object)(object)stick)
							};
						}
					}
				}
			}
			catch (Exception arg)
			{
				Logging.LogError($"Error in Puck_OnCollisionEnter_Patch Postfix().\n{arg}", ServerConfig);
			}
		}
	}

	[HarmonyPatch(typeof(Puck), "OnCollisionStay")]
	public class Puck_OnCollisionStay_Patch
	{
		[HarmonyPostfix]
		public static void Postfix(Puck __instance, Collision collision)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Invalid comparison between Unknown and I4
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_020e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0213: Unknown result type (might be due to invalid IL or missing references)
			//IL_0238: Unknown result type (might be due to invalid IL or missing references)
			//IL_0259: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_0104: Unknown result type (might be due to invalid IL or missing references)
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (!ServerFunc.IsDedicatedServer() || _paused || (int)NetworkBehaviourSingleton<GameManager>.Instance.Phase != 3 || !_logic)
				{
					return;
				}
				Stick stick = SystemFunc.GetStick(collision.gameObject);
				Player player;
				if (!Object.op_Implicit((Object)(object)stick))
				{
					PlayerBodyV2 playerBodyV = SystemFunc.GetPlayerBodyV2(collision.gameObject);
					if (!Object.op_Implicit((Object)(object)playerBodyV) || !Object.op_Implicit((Object)(object)playerBodyV.Player))
					{
						return;
					}
					player = playerBodyV.Player;
				}
				else
				{
					if (!Object.op_Implicit((Object)(object)stick.Player))
					{
						return;
					}
					player = stick.Player;
				}
				string text = ((object)player.SteamId.Value/*cast due to .constrained prefix*/).ToString();
				if (!_lastTimeOnCollisionStayOrExitWasCalled.TryGetValue(text, out var value))
				{
					value = new Stopwatch();
					value.Start();
					_lastTimeOnCollisionStayOrExitWasCalled.Add(text, value);
				}
				value.Restart();
				string item = _lastPlayerOnPuckTipIncludedSteamId[player.Team.Value].SteamId;
				if (text != item)
				{
					if (!string.IsNullOrEmpty(item) && _lastTeamOnPuckTipIncluded == player.Team.Value)
					{
						double totalMilliseconds = (DateTime.UtcNow - _lastPlayerOnPuckTipIncludedSteamId[player.Team.Value].Time).TotalMilliseconds;
						if (totalMilliseconds < 5000.0 && totalMilliseconds > 80.0)
						{
							if (!_passes.TryGetValue(item, out var _))
							{
								_passes.Add(item, 0);
							}
							_passes[item]++;
							NetworkCommunication.SendDataToAll("oomtm450_statsPASS" + item, _passes[item].ToString(), "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
							LogPass(item, _passes[item]);
						}
					}
					_lastPlayerOnPuckTipIncludedSteamId[player.Team.Value] = (text, DateTime.UtcNow);
				}
				_lastTeamOnPuckTipIncluded = player.Team.Value;
				if (!PuckFunc.PuckIsTipped(text, ServerConfig.MaxTippedMilliseconds, _playersCurrentPuckTouch, _lastTimeOnCollisionStayOrExitWasCalled, ((Component)__instance.Rigidbody).transform.position.y, 0.205f))
				{
					_lastPlayerOnPuckSteamId[player.Team.Value] = (text, DateTime.UtcNow);
				}
			}
			catch (Exception arg)
			{
				Logging.LogError($"Error in Puck_OnCollisionStay_Patch Postfix().\n{arg}", ServerConfig);
			}
		}
	}

	[HarmonyPatch(typeof(Puck), "OnCollisionExit")]
	public class Puck_OnCollisionExit_Patch
	{
		[HarmonyPostfix]
		public static void Postfix(Puck __instance, Collision collision)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Invalid comparison between Unknown and I4
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0093: Unknown result type (might be due to invalid IL or missing references)
			//IL_00df: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0104: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Unknown result type (might be due to invalid IL or missing references)
			//IL_014f: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (!ServerFunc.IsDedicatedServer() || _paused || (int)NetworkBehaviourSingleton<GameManager>.Instance.Phase != 3 || !_logic)
				{
					return;
				}
				Stick stick = SystemFunc.GetStick(collision.gameObject);
				if (!Object.op_Implicit((Object)(object)stick))
				{
					return;
				}
				_lastShotWasCounted[stick.Player.Team.Value] = false;
				_lastBlockWasCounted[stick.Player.Team.Value] = false;
				if (__instance.IsTouchingStick)
				{
					string text = ((object)stick.Player.SteamId.Value/*cast due to .constrained prefix*/).ToString();
					if (!_lastTimeOnCollisionStayOrExitWasCalled.TryGetValue(text, out var value))
					{
						value = new Stopwatch();
						value.Start();
						_lastTimeOnCollisionStayOrExitWasCalled.Add(text, value);
					}
					value.Restart();
					_lastPlayerOnPuckTipIncludedSteamId[stick.Player.Team.Value] = (text, DateTime.UtcNow);
					_lastTeamOnPuckTipIncluded = stick.Player.Team.Value;
					if (!PuckFunc.PuckIsTipped(text, ServerConfig.MaxTippedMilliseconds, _playersCurrentPuckTouch, _lastTimeOnCollisionStayOrExitWasCalled, ((Component)__instance.Rigidbody).transform.position.y, 0.205f))
					{
						_lastPlayerOnPuckSteamId[stick.Player.Team.Value] = (text, DateTime.UtcNow);
					}
				}
			}
			catch (Exception arg)
			{
				Logging.LogError($"Error in Puck_OnCollisionExit_Patch Postfix().\n{arg}", ServerConfig);
			}
		}
	}

	[HarmonyPatch(typeof(PlayerBodyV2), "OnCollisionEnter")]
	public class PlayerBodyV2_OnCollisionEnter_Patch
	{
		[HarmonyPostfix]
		public static void Postfix(PlayerBodyV2 __instance, Collision collision)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Invalid comparison between Unknown and I4
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
			if (!ServerFunc.IsDedicatedServer() || _paused || (int)NetworkBehaviourSingleton<GameManager>.Instance.Phase != 3 || !_logic)
			{
				return;
			}
			try
			{
				if (collision.gameObject.layer != LayerMask.NameToLayer("Player"))
				{
					return;
				}
				PlayerBodyV2 playerBodyV = SystemFunc.GetPlayerBodyV2(collision.gameObject);
				if (!Object.op_Implicit((Object)(object)playerBodyV) || !Object.op_Implicit((Object)(object)playerBodyV.Player) || !playerBodyV.Player.IsCharacterFullySpawned || !Object.op_Implicit((Object)(object)__instance) || !Object.op_Implicit((Object)(object)__instance.Player) || !__instance.Player.IsCharacterFullySpawned || playerBodyV.Player.Team.Value == __instance.Player.Team.Value)
				{
					return;
				}
				string text = ((object)playerBodyV.Player.SteamId.Value/*cast due to .constrained prefix*/).ToString();
				if (!_playerIsDown.TryGetValue(text, out var value))
				{
					value = false;
				}
				string text2 = ((object)__instance.Player.SteamId.Value/*cast due to .constrained prefix*/).ToString();
				bool value2;
				if (!value && (playerBodyV.HasFallen || playerBodyV.HasSlipped))
				{
					if (_playerIsDown.TryGetValue(text, out value2))
					{
						_playerIsDown[text] = true;
					}
					else
					{
						_playerIsDown.Add(text, value: true);
					}
					if (__instance.Player.PlayerBody.HasFallen || __instance.Player.PlayerBody.HasSlipped)
					{
						if (_playerIsDown.TryGetValue(text2, out value2))
						{
							_playerIsDown[text2] = true;
						}
						else
						{
							_playerIsDown.Add(text2, value: true);
						}
						return;
					}
					ProcessHit(((object)__instance.Player.SteamId.Value/*cast due to .constrained prefix*/).ToString());
				}
				if (__instance.Player.PlayerBody.HasFallen || __instance.Player.PlayerBody.HasSlipped)
				{
					if (_playerIsDown.TryGetValue(text2, out value2))
					{
						_playerIsDown[text2] = true;
					}
					else
					{
						_playerIsDown.Add(text2, value: true);
					}
				}
			}
			catch (Exception arg)
			{
				Logging.LogError(string.Format("Error in {0} Postfix().\n{1}", "PlayerBodyV2_OnCollisionEnter_Patch", arg), ServerConfig);
			}
		}
	}

	[HarmonyPatch(typeof(PlayerBodyV2), "OnStandUp")]
	public class PlayerBodyV2_OnStandUp_Patch
	{
		[HarmonyPostfix]
		public static void Postfix(PlayerBodyV2 __instance)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Invalid comparison between Unknown and I4
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			if (!ServerFunc.IsDedicatedServer() || _paused || (int)NetworkBehaviourSingleton<GameManager>.Instance.Phase != 3 || !_logic)
			{
				return;
			}
			try
			{
				string text = ((object)__instance.Player.SteamId.Value/*cast due to .constrained prefix*/).ToString();
				if (_playerIsDown.TryGetValue(text, out var _))
				{
					_playerIsDown[text] = false;
				}
				else
				{
					_playerIsDown.Add(text, value: false);
				}
			}
			catch (Exception arg)
			{
				Logging.LogError(string.Format("Error in {0} Postfix().\n{1}", "PlayerBodyV2_OnStandUp_Patch", arg), ServerConfig);
			}
		}
	}

	[HarmonyPatch(typeof(GameManager), "Server_SetPhase")]
	public class GameManager_Server_SetPhase_Patch
	{
		[HarmonyPrefix]
		public static bool Prefix(GameManager __instance, GamePhase phase, ref int time)
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Invalid comparison between Unknown and I4
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Invalid comparison between Unknown and I4
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Invalid comparison between Unknown and I4
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_0303: Unknown result type (might be due to invalid IL or missing references)
			//IL_030c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_0106: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			//IL_0145: Unknown result type (might be due to invalid IL or missing references)
			//IL_014c: Unknown result type (might be due to invalid IL or missing references)
			//IL_089c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0904: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f3: Invalid comparison between Unknown and I4
			//IL_0200: Unknown result type (might be due to invalid IL or missing references)
			//IL_0208: Unknown result type (might be due to invalid IL or missing references)
			//IL_0218: Unknown result type (might be due to invalid IL or missing references)
			//IL_0246: Unknown result type (might be due to invalid IL or missing references)
			//IL_0253: Unknown result type (might be due to invalid IL or missing references)
			//IL_0225: Unknown result type (might be due to invalid IL or missing references)
			//IL_0232: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				if (!ServerFunc.IsDedicatedServer() || !_logic)
				{
					return true;
				}
				if ((int)phase == 2 || (int)phase == 1 || (int)phase == 8)
				{
					ResetPuckWasSavedOrBlockedChecks();
					_puckLastCoordinate = Vector3.zero;
					_puckZCoordinateDifference = 0f;
					foreach (PlayerTeam item in new List<PlayerTeam>(_lastPlayerOnPuckTipIncludedSteamId.Keys))
					{
						_lastPlayerOnPuckTipIncludedSteamId[item] = ("", DateTime.MinValue);
					}
					foreach (PlayerTeam item2 in new List<PlayerTeam>(_lastPlayerOnPuckSteamId.Keys))
					{
						_lastPlayerOnPuckSteamId[item2] = ("", DateTime.MinValue);
					}
					foreach (PlayerTeam item3 in new List<PlayerTeam>(_lastShotWasCounted.Keys))
					{
						_lastShotWasCounted[item3] = true;
					}
					foreach (PlayerTeam item4 in new List<PlayerTeam>(_lastBlockWasCounted.Keys))
					{
						_lastBlockWasCounted[item4] = true;
					}
					foreach (Stopwatch value27 in _lastTimeOnCollisionStayOrExitWasCalled.Values)
					{
						value27.Stop();
					}
					_lastTimeOnCollisionStayOrExitWasCalled.Clear();
					foreach (Stopwatch value28 in _playersCurrentPuckTouch.Values)
					{
						value28.Stop();
					}
					_playersCurrentPuckTouch.Clear();
					if ((int)phase == 8)
					{
						string text = "";
						PlayerTeam val = (PlayerTeam)0;
						try
						{
							if (__instance.GameState.Value.BlueScore > __instance.GameState.Value.RedScore)
							{
								val = (PlayerTeam)2;
								text = _blueGoals[__instance.GameState.Value.RedScore];
							}
							else
							{
								val = (PlayerTeam)3;
								text = _redGoals[__instance.GameState.Value.BlueScore];
							}
							LogGWG(text);
						}
						catch (IndexOutOfRangeException)
						{
						}
						catch (ArgumentOutOfRangeException)
						{
						}
						Dictionary<string, double> dictionary = new Dictionary<string, double>();
						foreach (Player player in NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers(false))
						{
							if ((Object)(object)player == (Object)null || !Object.op_Implicit((Object)(object)player))
							{
								continue;
							}
							string text2 = ((object)player.SteamId.Value/*cast due to .constrained prefix*/).ToString();
							dictionary.Add(text2, 0.0);
							double num = ((text == text2) ? 0.5 : 0.0);
							double num2 = ((val == player.Team.Value) ? 1.1 : 1.0);
							if (PlayerFunc.IsGoalie(player))
							{
								if (_savePerc.TryGetValue(text2, out (int, int) value))
								{
									dictionary[text2] += ((double)value.Item1 / (double)value.Item2 - 0.6) * (double)value.Item1 * 18.6;
								}
								if (_sog.TryGetValue(text2, out var value2))
								{
									dictionary[text2] += (double)value2 * 1.0;
								}
								if (_passes.TryGetValue(text2, out var value3))
								{
									dictionary[text2] += (double)value3 * 2.0;
								}
								dictionary[text2] += 175.0 * num;
								dictionary[text2] += (double)player.Goals.Value * 175.0;
								dictionary[text2] += (double)player.Assists.Value * 35.0;
							}
							else
							{
								if (_sog.TryGetValue(text2, out var value4))
								{
									dictionary[text2] += (double)value4 * 5.0;
									dictionary[text2] += ((double)(player.Goals.Value + 1) / (double)value4 - 0.25) * (double)value4 * 4.0;
								}
								if (_passes.TryGetValue(text2, out var value5))
								{
									dictionary[text2] += (double)value5 * 0.5;
								}
								if (_blocks.TryGetValue(text2, out var value6))
								{
									dictionary[text2] += (double)value6 * 5.0;
								}
								dictionary[text2] += 70.0 * num;
								dictionary[text2] += (double)player.Goals.Value * 70.0;
								dictionary[text2] += (double)player.Assists.Value * 30.0;
							}
							if (_hits.TryGetValue(text2, out var value7))
							{
								dictionary[text2] += (double)value7 * 0.2;
							}
							if (_takeaways.TryGetValue(text2, out var value8))
							{
								dictionary[text2] += (double)value8 * 0.2;
							}
							if (_turnovers.TryGetValue(text2, out var value9))
							{
								dictionary[text2] -= (double)value9 * 0.2;
							}
							if (_plusMinus.TryGetValue(text2, out var value10))
							{
								dictionary[text2] += (double)value10 * 5.0;
							}
							dictionary[text2] *= num2;
						}
						dictionary = dictionary.OrderByDescending((KeyValuePair<string, double> x) => x.Value).ToDictionary((KeyValuePair<string, double> x) => x.Key, (KeyValuePair<string, double> x) => x.Value);
						if (dictionary.Count != 0)
						{
							NetworkBehaviourSingleton<UIChat>.Instance.Server_SendSystemChatMessage("STARS OF THE MATCH");
							_stars[1] = dictionary.ElementAt(0).Key;
						}
						else
						{
							_stars[1] = "";
						}
						if (dictionary.Count > 1)
						{
							_stars[2] = dictionary.ElementAt(1).Key;
						}
						else
						{
							_stars[2] = "";
						}
						if (dictionary.Count > 2)
						{
							_stars[3] = dictionary.ElementAt(2).Key;
						}
						else
						{
							_stars[3] = "";
						}
						foreach (KeyValuePair<int, string> item5 in _stars.OrderByDescending((KeyValuePair<int, string> x) => x.Key))
						{
							if (!string.IsNullOrEmpty(item5.Value))
							{
								Player playerBySteamId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(FixedString32Bytes.op_Implicit(item5.Value));
								if ((Object)(object)playerBySteamId != (Object)null && Object.op_Implicit((Object)(object)playerBySteamId))
								{
									NetworkBehaviourSingleton<UIChat>.Instance.Server_SendSystemChatMessage(string.Format("The {0} star is... #{1} {2} !", (item5.Key == 1) ? "first" : ((item5.Key == 2) ? "second" : "third"), playerBySteamId.Number.Value, playerBySteamId.Username.Value));
								}
								NetworkCommunication.SendDataToAll("oomtm450_statsSTAR", $"{item5.Value};{item5.Key}", "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
								LogStar(item5.Value, item5.Key);
							}
						}
						Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
						foreach (var (key, value11) in _playersInfo.Values)
						{
							dictionary2.Add(key, value11);
						}
						Dictionary<string, (string, int)> dictionary3 = new Dictionary<string, (string, int)>();
						foreach (KeyValuePair<string, int> item6 in _sog)
						{
							dictionary3.Add(item6.Key, (dictionary2.TryGetValue(item6.Key, out var value12) ? value12 : "", item6.Value));
						}
						Dictionary<string, (string, int)> dictionary4 = new Dictionary<string, (string, int)>();
						foreach (KeyValuePair<string, int> pass in _passes)
						{
							dictionary4.Add(pass.Key, (dictionary2.TryGetValue(pass.Key, out var value13) ? value13 : "", pass.Value));
						}
						Dictionary<string, (string, int)> dictionary5 = new Dictionary<string, (string, int)>();
						foreach (KeyValuePair<string, int> block in _blocks)
						{
							dictionary5.Add(block.Key, (dictionary2.TryGetValue(block.Key, out var value14) ? value14 : "", block.Value));
						}
						Dictionary<string, (string, int)> dictionary6 = new Dictionary<string, (string, int)>();
						foreach (KeyValuePair<string, int> hit in _hits)
						{
							dictionary6.Add(hit.Key, (dictionary2.TryGetValue(hit.Key, out var value15) ? value15 : "", hit.Value));
						}
						Dictionary<string, (string, int)> dictionary7 = new Dictionary<string, (string, int)>();
						foreach (KeyValuePair<string, int> takeaway in _takeaways)
						{
							dictionary7.Add(takeaway.Key, (dictionary2.TryGetValue(takeaway.Key, out var value16) ? value16 : "", takeaway.Value));
						}
						Dictionary<string, (string, int)> dictionary8 = new Dictionary<string, (string, int)>();
						foreach (KeyValuePair<string, int> turnover in _turnovers)
						{
							dictionary8.Add(turnover.Key, (dictionary2.TryGetValue(turnover.Key, out var value17) ? value17 : "", turnover.Value));
						}
						Dictionary<string, (string, (int, int))> dictionary9 = new Dictionary<string, (string, (int, int))>();
						foreach (KeyValuePair<string, (int, int)> item7 in _savePerc)
						{
							dictionary9.Add(item7.Key, (dictionary2.TryGetValue(item7.Key, out var value18) ? value18 : "", item7.Value));
						}
						Dictionary<string, (string, int)> dictionary10 = new Dictionary<string, (string, int)>();
						foreach (KeyValuePair<string, int> stickSafe in _stickSaves)
						{
							dictionary10.Add(stickSafe.Key, (dictionary2.TryGetValue(stickSafe.Key, out var value19) ? value19 : "", stickSafe.Value));
						}
						List<string> list = new List<string>();
						foreach (string blueGoal in _blueGoals)
						{
							list.Add(blueGoal + "," + (dictionary2.TryGetValue(blueGoal, out var value20) ? value20 : ""));
						}
						List<string> list2 = new List<string>();
						foreach (string redGoal in _redGoals)
						{
							list2.Add(redGoal + "," + (dictionary2.TryGetValue(redGoal, out var value21) ? value21 : ""));
						}
						List<string> list3 = new List<string>();
						foreach (string blueAssist in _blueAssists)
						{
							list3.Add(blueAssist + "," + (dictionary2.TryGetValue(blueAssist, out var value22) ? value22 : ""));
						}
						List<string> list4 = new List<string>();
						foreach (string redAssist in _redAssists)
						{
							list4.Add(redAssist + "," + (dictionary2.TryGetValue(redAssist, out var value23) ? value23 : ""));
						}
						Dictionary<int, (string, string)> dictionary11 = new Dictionary<int, (string, string)>();
						foreach (KeyValuePair<int, string> star in _stars)
						{
							dictionary11.Add(star.Key, (star.Value, dictionary2.TryGetValue(star.Value, out var value24) ? value24 : ""));
						}
						Dictionary<string, (string, int)> dictionary12 = new Dictionary<string, (string, int)>();
						foreach (KeyValuePair<string, int> plusMinu in _plusMinus)
						{
							dictionary12.Add(plusMinu.Key, (dictionary2.TryGetValue(plusMinu.Key, out var value25) ? value25 : "", plusMinu.Value));
						}
						string value26;
						string text3 = JsonConvert.SerializeObject((object)new Dictionary<string, object>
						{
							{ "sog", dictionary3 },
							{ "passes", dictionary4 },
							{ "blocks", dictionary5 },
							{ "hits", dictionary6 },
							{ "takeaways", dictionary7 },
							{ "turnovers", dictionary8 },
							{ "saveperc", dictionary9 },
							{ "sticksaves", dictionary10 },
							{ "bluegoals", list },
							{ "redgoals", list2 },
							{ "blueassists", list3 },
							{ "redassists", list4 },
							{
								"gwg",
								text + "," + (dictionary2.TryGetValue(text, out value26) ? value26 : "")
							},
							{ "stars", dictionary11 },
							{ "plusminus", dictionary12 }
						}, (Formatting)1);
						Logging.Log("Stats:" + text3, ServerConfig);
						if (ServerConfig.SaveEOGJSON)
						{
							try
							{
								string text4 = Path.Combine(Path.GetFullPath("."), "stats");
								if (!Directory.Exists(text4))
								{
									Directory.CreateDirectory(text4);
								}
								File.WriteAllText(Path.Combine(text4, "oomtm450_stats_" + DateTime.UtcNow.ToString("dd-MM-yyyy_HH-mm-ss") + ".json"), text3);
							}
							catch (Exception arg)
							{
								Logging.LogError($"Can't write the end of game stats in the stats folder. (Permission error ?)\n{arg}", ServerConfig);
							}
						}
					}
				}
			}
			catch (Exception arg2)
			{
				Logging.LogError(string.Format("Error in {0} Prefix().\n{1}", "GameManager_Server_SetPhase_Patch", arg2), ServerConfig);
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(UIChat), "WrapPlayerUsername")]
	public static class UIChat_WrapPlayerUsername_Patch
	{
		public static void Postfix(Player player, ref string __result)
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			if (!((Object)(object)player == (Object)null) && Object.op_Implicit((Object)(object)player))
			{
				string text = ((object)player.SteamId.Value/*cast due to .constrained prefix*/).ToString();
				if (!string.IsNullOrEmpty(text))
				{
					__result = GetStarTag(text) + __result;
				}
			}
		}
	}

	internal abstract class Check
	{
		internal bool HasToCheck { get; set; }

		internal int FramesChecked { get; set; }
	}

	internal class SaveCheck : Check
	{
		internal string ShooterSteamId { get; set; } = "";

		internal PlayerTeam ShooterTeam { get; set; } = (PlayerTeam)2;

		internal bool HitStick { get; set; }
	}

	internal class BlockCheck : Check
	{
		internal string BlockerSteamId { get; set; } = "";

		internal PlayerTeam ShooterTeam { get; set; } = (PlayerTeam)2;
	}

	internal class Possession
	{
		internal string SteamId { get; set; } = "";

		internal PlayerTeam Team { get; set; }

		internal DateTime Date { get; set; } = DateTime.MinValue;
	}

	private static readonly string MOD_VERSION = "0.7.2";

	private static readonly ReadOnlyCollection<string> OLD_MOD_VERSIONS = new ReadOnlyCollection<string>(new List<string>
	{
		"0.1.0", "0.1.1", "0.1.2", "0.2.0", "0.2.1", "0.2.2", "0.3.0", "0.4.0", "0.4.1", "0.5.0",
		"0.6.0", "0.7.0", "0.7.1"
	});

	private static readonly ReadOnlyCollection<string> DATA_NAMES_TO_IGNORE = new ReadOnlyCollection<string>(new List<string> { "eventName", "nextfaceoff", "oomtm450_statsPLUSMINUS", "oomtm450_statsTAKEAWAY", "oomtm450_statsTURNOVER", "oomtm450_statsBLOCK", "oomtm450_statsHIT", "oomtm450_statsPASS", "oomtm450_statsSOG", "oomtm450_statsSAVEPERC" });

	private const string BATCH_SOG = "oomtm450_statsBATCHSOG";

	private const string BATCH_SAVEPERC = "oomtm450_statsBATCHSAVEPERC";

	private const string BATCH_BLOCK = "oomtm450_statsBATCHBLOCK";

	private const string BATCH_HIT = "oomtm450_statsBATCHHIT";

	private const string BATCH_TAKEAWAY = "oomtm450_statsBATCHTAKEAWAY";

	private const string BATCH_TURNOVER = "oomtm450_statsBATCHTURNOVER";

	private const string BATCH_PASS = "oomtm450_statsBATCHPASS";

	private const string BATCH_PLUSMINUS = "oomtm450_statsBATCHPLUSMINUS";

	private const string RESET_ALL = "oomtm450_statsRESETALL";

	private const string STAR = "oomtm450_statsSTAR";

	private const string SOG_HEADER_LABEL_NAME = "SOGHeaderLabel";

	private const string SOG_LABEL = "SOGLabel";

	private static bool? _rulesetModEnabled = null;

	private static bool _sendSavePercDuringGoalNextFrame = false;

	private static Player _sendSavePercDuringGoalNextFrame_Player = null;

	private static Vector3 _puckLastCoordinate = Vector3.zero;

	private static float _puckZCoordinateDifference = 0f;

	private static readonly LockDictionary<ulong, (string SteamId, string Username)> _playersInfo = new LockDictionary<ulong, (string, string)>();

	private static readonly LockDictionary<ulong, DateTime> _sentOutOfDateMessage = new LockDictionary<ulong, DateTime>();

	private static readonly LockDictionary<PlayerTeam, SaveCheck> _checkIfPuckWasSaved = new LockDictionary<PlayerTeam, SaveCheck>
	{
		{
			(PlayerTeam)2,
			new SaveCheck()
		},
		{
			(PlayerTeam)3,
			new SaveCheck()
		}
	};

	private static readonly LockDictionary<PlayerTeam, BlockCheck> _checkIfPuckWasBlocked = new LockDictionary<PlayerTeam, BlockCheck>
	{
		{
			(PlayerTeam)2,
			new BlockCheck()
		},
		{
			(PlayerTeam)3,
			new BlockCheck()
		}
	};

	private static readonly LockDictionary<PlayerTeam, bool> _lastShotWasCounted = new LockDictionary<PlayerTeam, bool>
	{
		{
			(PlayerTeam)2,
			true
		},
		{
			(PlayerTeam)3,
			true
		}
	};

	private static readonly LockDictionary<PlayerTeam, bool> _lastBlockWasCounted = new LockDictionary<PlayerTeam, bool>
	{
		{
			(PlayerTeam)2,
			true
		},
		{
			(PlayerTeam)3,
			true
		}
	};

	private static readonly LockDictionary<PlayerTeam, (string SteamId, DateTime Time)> _lastPlayerOnPuckTipIncludedSteamId = new LockDictionary<PlayerTeam, (string, DateTime)>
	{
		{
			(PlayerTeam)2,
			("", DateTime.MinValue)
		},
		{
			(PlayerTeam)3,
			("", DateTime.MinValue)
		}
	};

	private static readonly LockDictionary<PlayerTeam, (string SteamId, DateTime Time)> _lastPlayerOnPuckSteamId = new LockDictionary<PlayerTeam, (string, DateTime)>
	{
		{
			(PlayerTeam)2,
			("", DateTime.MinValue)
		},
		{
			(PlayerTeam)3,
			("", DateTime.MinValue)
		}
	};

	private static readonly LockDictionary<string, Stopwatch> _playersCurrentPuckTouch = new LockDictionary<string, Stopwatch>();

	private static readonly LockDictionary<string, Stopwatch> _lastTimeOnCollisionStayOrExitWasCalled = new LockDictionary<string, Stopwatch>();

	private static readonly LockDictionary<string, bool> _playerIsDown = new LockDictionary<string, bool>();

	private static Possession _lastPossession = new Possession();

	private static PlayerTeam _lastTeamOnPuckTipIncluded = (PlayerTeam)2;

	private static PuckRaycast _puckRaycast;

	private static bool _paused = false;

	private static bool _logic = true;

	private static readonly Harmony _harmony = new Harmony("oomtm450_stats");

	private static bool _harmonyPatched = false;

	private static bool _hasRegisteredWithNamedMessageHandler = false;

	private static readonly LockDictionary<string, int> _sog = new LockDictionary<string, int>();

	private static readonly LockDictionary<string, (int Saves, int Shots)> _savePerc = new LockDictionary<string, (int, int)>();

	private static readonly LockDictionary<string, int> _stickSaves = new LockDictionary<string, int>();

	private static readonly LockDictionary<string, int> _blocks = new LockDictionary<string, int>();

	private static readonly LockDictionary<string, int> _hits = new LockDictionary<string, int>();

	private static readonly LockDictionary<string, int> _takeaways = new LockDictionary<string, int>();

	private static readonly LockDictionary<string, int> _turnovers = new LockDictionary<string, int>();

	private static readonly LockDictionary<string, int> _passes = new LockDictionary<string, int>();

	private static readonly LockList<string> _blueGoals = new LockList<string>();

	private static readonly LockList<string> _redGoals = new LockList<string>();

	private static readonly LockList<string> _blueAssists = new LockList<string>();

	private static readonly LockList<string> _redAssists = new LockList<string>();

	private static readonly LockDictionary<int, string> _stars = new LockDictionary<int, string>
	{
		{ 1, "" },
		{ 2, "" },
		{ 3, "" }
	};

	private static readonly LockDictionary<string, int> _plusMinus = new LockDictionary<string, int>();

	internal static ClientConfig _clientConfig = new ClientConfig();

	private static DateTime _lastDateTimeAskStartupData = DateTime.MinValue;

	private static bool _serverHasResponded = false;

	private static bool _askForKick = false;

	private static bool _addServerModVersionOutOfDateMessage = false;

	private static int _askServerForStartupDataCount = 0;

	private static readonly List<string> _hasUpdatedUIScoreboard = new List<string>();

	private static readonly LockDictionary<string, Label> _sogLabels = new LockDictionary<string, Label>();

	internal static ServerConfig ServerConfig { get; set; } = new ServerConfig();

	public bool OnEnable()
	{
		try
		{
			if (_harmonyPatched)
			{
				return true;
			}
			Logging.Log("Enabling...", ServerConfig, bypassConfig: true);
			if (Application.version != "202")
			{
				Logging.Log("Server game version is " + Application.version + " and not 202. Mod will not be enabled.", ServerConfig);
				return false;
			}
			_harmony.PatchAll();
			Logging.Log("Enabled.", ServerConfig, bypassConfig: true);
			NetworkCommunication.AddToNotLogList(DATA_NAMES_TO_IGNORE);
			if (ServerFunc.IsDedicatedServer())
			{
				Server_RegisterNamedMessageHandler();
				Logging.Log("Setting server sided config.", ServerConfig, bypassConfig: true);
				ServerConfig = ServerConfig.ReadConfig();
			}
			else
			{
				Logging.Log("Setting client sided config.", ServerConfig, bypassConfig: true);
				_clientConfig = ClientConfig.ReadConfig();
			}
			Logging.Log("Subscribing to events.", ServerConfig, bypassConfig: true);
			if (ServerFunc.IsDedicatedServer())
			{
				MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnClientConnected", (Action<Dictionary<string, object>>)Event_OnClientConnected);
				MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnClientDisconnected", (Action<Dictionary<string, object>>)Event_OnClientDisconnected);
				MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerRoleChanged", (Action<Dictionary<string, object>>)Event_OnPlayerRoleChanged);
				MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("oomtm450_stats", (Action<Dictionary<string, object>>)Event_OnStatsTrigger);
				MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("oomtm450_ruleset", (Action<Dictionary<string, object>>)Event_OnRulesetTrigger);
			}
			else
			{
				MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnClientStopped", (Action<Dictionary<string, object>>)Event_Client_OnClientStopped);
			}
			_harmonyPatched = true;
			_logic = true;
			return true;
		}
		catch (Exception arg)
		{
			Logging.LogError($"Failed to enable.\n{arg}", ServerConfig);
			return false;
		}
	}

	public bool OnDisable()
	{
		try
		{
			if (!_harmonyPatched)
			{
				return true;
			}
			Logging.Log("Disabling...", ServerConfig, bypassConfig: true);
			Logging.Log("Unsubscribing from events.", ServerConfig, bypassConfig: true);
			NetworkCommunication.RemoveFromNotLogList(DATA_NAMES_TO_IGNORE);
			if (ServerFunc.IsDedicatedServer())
			{
				MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnClientConnected", (Action<Dictionary<string, object>>)Event_OnClientConnected);
				MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnClientDisconnected", (Action<Dictionary<string, object>>)Event_OnClientDisconnected);
				MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerRoleChanged", (Action<Dictionary<string, object>>)Event_OnPlayerRoleChanged);
				MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("oomtm450_stats", (Action<Dictionary<string, object>>)Event_OnStatsTrigger);
				MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("oomtm450_ruleset", (Action<Dictionary<string, object>>)Event_OnRulesetTrigger);
				NetworkManager singleton = NetworkManager.Singleton;
				if (singleton != null)
				{
					CustomMessagingManager customMessagingManager = singleton.CustomMessagingManager;
					if (customMessagingManager != null)
					{
						customMessagingManager.UnregisterNamedMessageHandler("oomtm450_stats_client");
					}
				}
			}
			else
			{
				MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnClientStopped", (Action<Dictionary<string, object>>)Event_Client_OnClientStopped);
				Event_Client_OnClientStopped(new Dictionary<string, object>());
				NetworkManager singleton2 = NetworkManager.Singleton;
				if (singleton2 != null)
				{
					CustomMessagingManager customMessagingManager2 = singleton2.CustomMessagingManager;
					if (customMessagingManager2 != null)
					{
						customMessagingManager2.UnregisterNamedMessageHandler("oomtm450_stats_server");
					}
				}
			}
			_hasRegisteredWithNamedMessageHandler = false;
			_rulesetModEnabled = null;
			_serverHasResponded = false;
			_askServerForStartupDataCount = 0;
			ScoreboardModifications(enable: false);
			_harmony.UnpatchSelf();
			Logging.Log("Disabled.", ServerConfig, bypassConfig: true);
			_harmonyPatched = false;
			_logic = true;
			return true;
		}
		catch (Exception arg)
		{
			Logging.LogError($"Failed to disable.\n{arg}", ServerConfig);
			return false;
		}
	}

	public static void Event_OnStatsTrigger(Dictionary<string, object> message)
	{
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			foreach (KeyValuePair<string, object> item in message)
			{
				string text = (string)item.Value;
				if (!NetworkCommunication.GetDataNamesToIgnore().Contains(item.Key))
				{
					Logging.Log("Received data " + item.Key + ". Content : " + text, ServerConfig);
				}
				string key = item.Key;
				if (!(key == "oomtm450_statsSOG"))
				{
					if (key == "logic")
					{
						_logic = bool.Parse(text);
					}
					continue;
				}
				_sendSavePercDuringGoalNextFrame_Player = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(FixedString32Bytes.op_Implicit(text));
				if ((Object)(object)_sendSavePercDuringGoalNextFrame_Player == (Object)null || !Object.op_Implicit((Object)(object)_sendSavePercDuringGoalNextFrame_Player))
				{
					Logging.LogError("_sendSavePercDuringGoalNextFrame_Player is null.", ServerConfig);
				}
				else
				{
					_sendSavePercDuringGoalNextFrame = true;
				}
			}
		}
		catch (Exception arg)
		{
			Logging.LogError(string.Format("Error in {0}.\n{1}", "Event_OnStatsTrigger", arg), ServerConfig);
		}
	}

	public static void Event_OnRulesetTrigger(Dictionary<string, object> message)
	{
		try
		{
			foreach (KeyValuePair<string, object> item in message)
			{
				string text = (string)item.Value;
				if (!NetworkCommunication.GetDataNamesToIgnore().Contains(item.Key))
				{
					Logging.Log("Received data " + item.Key + ". Content : " + text, ServerConfig);
				}
				if (item.Key == "pause")
				{
					_paused = bool.Parse(text);
				}
			}
		}
		catch (Exception arg)
		{
			Logging.LogError(string.Format("Error in {0}.\n{1}", "Event_OnRulesetTrigger", arg), ServerConfig);
		}
	}

	public static void Event_OnClientConnected(Dictionary<string, object> message)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		if (!ServerFunc.IsDedicatedServer())
		{
			return;
		}
		try
		{
			Server_RegisterNamedMessageHandler();
			ulong num = (ulong)message["clientId"];
			((object)NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(num).SteamId.Value/*cast due to .constrained prefix*/).ToString();
			try
			{
				_playersInfo.Add(num, ("", ""));
			}
			catch
			{
				_playersInfo.Remove(num);
				_playersInfo.Add(num, ("", ""));
			}
			CheckForRulesetMod();
		}
		catch (Exception arg)
		{
			Logging.LogError(string.Format("Error in {0}.\n{1}", "Event_OnClientConnected", arg), ServerConfig);
		}
	}

	public static void Event_OnClientDisconnected(Dictionary<string, object> message)
	{
		if (!ServerFunc.IsDedicatedServer())
		{
			return;
		}
		try
		{
			ulong num = (ulong)message["clientId"];
			string item;
			try
			{
				item = _playersInfo[num].SteamId;
			}
			catch
			{
				Logging.LogError(string.Format("Client Id {0} steam Id not found in {1}.", num, "_playersInfo"), ServerConfig);
				return;
			}
			_sentOutOfDateMessage.Remove(num);
			_playerIsDown.Remove(item);
			_playersCurrentPuckTouch.Remove(item);
			_lastTimeOnCollisionStayOrExitWasCalled.Remove(item);
			_playersInfo.Remove(num);
		}
		catch (Exception arg)
		{
			Logging.LogError(string.Format("Error in {0}.\n{1}", "Event_OnClientDisconnected", arg), ServerConfig);
		}
	}

	public static void Event_Client_OnClientStopped(Dictionary<string, object> message)
	{
		if ((Object)(object)NetworkManager.Singleton == (Object)null || ServerFunc.IsDedicatedServer())
		{
			return;
		}
		try
		{
			ServerConfig = new ServerConfig();
			_serverHasResponded = false;
			_askServerForStartupDataCount = 0;
			foreach (int item in new List<int>(_stars.Keys))
			{
				_stars[item] = "";
			}
			_stickSaves.Clear();
			_passes.Clear();
			_blocks.Clear();
			_hits.Clear();
			_takeaways.Clear();
			_turnovers.Clear();
			_blueGoals.Clear();
			_blueAssists.Clear();
			_redGoals.Clear();
			_redAssists.Clear();
			_plusMinus.Clear();
			ScoreboardModifications(enable: false);
		}
		catch (Exception arg)
		{
			Logging.LogError(string.Format("Error in {0}.\n{1}", "Event_Client_OnClientStopped", arg), _clientConfig);
		}
	}

	public static void Event_OnPlayerRoleChanged(Dictionary<string, object> message)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Invalid comparison between Unknown and I4
		Dictionary<ulong, (string, string)> dictionary = new Dictionary<ulong, (string, string)>();
		foreach (KeyValuePair<ulong, (string, string)> item in _playersInfo)
		{
			if (string.IsNullOrEmpty(item.Value.Item1))
			{
				Player playerByClientId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(item.Key);
				dictionary.Add(item.Key, (((object)playerByClientId.SteamId.Value/*cast due to .constrained prefix*/).ToString(), ((object)playerByClientId.Username.Value/*cast due to .constrained prefix*/).ToString()));
			}
		}
		foreach (KeyValuePair<ulong, (string, string)> item2 in dictionary)
		{
			if (!string.IsNullOrEmpty(item2.Value.Item1))
			{
				_playersInfo[item2.Key] = item2.Value;
				Logging.Log($"Added clientId {item2.Key} linked to Steam Id {item2.Value.Item1} ({item2.Value.Item2}).", ServerConfig);
			}
		}
		string text = ((object)((Player)message["player"]).SteamId.Value/*cast due to .constrained prefix*/).ToString();
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		if ((int)(PlayerRole)message["newRole"] != 2)
		{
			if (!_sog.TryGetValue(text, out var _))
			{
				_sog.Add(text, 0);
			}
			NetworkCommunication.SendDataToAll("oomtm450_statsSOG" + text, _sog[text].ToString(), "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
		}
		else
		{
			if (!_savePerc.TryGetValue(text, out (int, int) _))
			{
				_savePerc.Add(text, (0, 0));
			}
			NetworkCommunication.SendDataToAll("oomtm450_statsSAVEPERC" + text, _savePerc[text].ToString(), "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
		}
	}

	private static void ProcessHit(string hitterSteamId)
	{
		if (!_hits.TryGetValue(hitterSteamId, out var _))
		{
			_hits.Add(hitterSteamId, 0);
		}
		_hits[hitterSteamId]++;
		NetworkCommunication.SendDataToAll("oomtm450_statsHIT" + hitterSteamId, _hits[hitterSteamId].ToString(), "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
		LogHit(hitterSteamId, _hits[hitterSteamId]);
	}

	private static void ProcessBlock(string blockerSteamId)
	{
		if (!_blocks.TryGetValue(blockerSteamId, out var _))
		{
			_blocks.Add(blockerSteamId, 0);
		}
		_blocks[blockerSteamId]++;
		NetworkCommunication.SendDataToAll("oomtm450_statsBLOCK" + blockerSteamId, _blocks[blockerSteamId].ToString(), "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
		LogBlock(blockerSteamId, _blocks[blockerSteamId]);
	}

	private static void ProcessTakeaways(string takeawaySteamId)
	{
		if (!_takeaways.TryGetValue(takeawaySteamId, out var _))
		{
			_takeaways.Add(takeawaySteamId, 0);
		}
		_takeaways[takeawaySteamId]++;
		NetworkCommunication.SendDataToAll("oomtm450_statsTAKEAWAY" + takeawaySteamId, _takeaways[takeawaySteamId].ToString(), "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
		LogTakeaways(takeawaySteamId, _takeaways[takeawaySteamId]);
	}

	private static void ProcessTurnovers(string turnoverSteamId)
	{
		if (!_turnovers.TryGetValue(turnoverSteamId, out var _))
		{
			_turnovers.Add(turnoverSteamId, 0);
		}
		_turnovers[turnoverSteamId]++;
		NetworkCommunication.SendDataToAll("oomtm450_statsTURNOVER" + turnoverSteamId, _turnovers[turnoverSteamId].ToString(), "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
		LogTurnovers(turnoverSteamId, _turnovers[turnoverSteamId]);
	}

	private static void Server_RegisterNamedMessageHandler()
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		if ((Object)(object)NetworkManager.Singleton != (Object)null && NetworkManager.Singleton.CustomMessagingManager != null && !_hasRegisteredWithNamedMessageHandler)
		{
			Logging.Log("RegisterNamedMessageHandler oomtm450_stats_client.", ServerConfig);
			NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("oomtm450_stats_client", new HandleNamedMessageDelegate(ReceiveData));
			_hasRegisteredWithNamedMessageHandler = true;
		}
	}

	private static void CheckForRulesetMod()
	{
		if (!((Object)(object)MonoBehaviourSingleton<ModManagerV2>.Instance == (Object)null) && MonoBehaviourSingleton<ModManagerV2>.Instance.EnabledModIds != null && (!_rulesetModEnabled.HasValue || !_rulesetModEnabled.Value))
		{
			_rulesetModEnabled = MonoBehaviourSingleton<ModManagerV2>.Instance.EnabledModIds.Contains(3501446576uL) || MonoBehaviourSingleton<ModManagerV2>.Instance.EnabledModIds.Contains(3500559233uL);
			Logging.Log($"Ruleset mod is enabled : {_rulesetModEnabled}.", ServerConfig, bypassConfig: true);
		}
	}

	public static void ReceiveData(ulong clientId, FastBufferReader reader)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			string text;
			string text2;
			if (clientId != 0L)
			{
				(text, text2) = NetworkCommunication.GetData(clientId, reader, ServerConfig);
			}
			else
			{
				(text, text2) = NetworkCommunication.GetData(clientId, reader, _clientConfig);
			}
			switch (text)
			{
			case "oomtm450_stats_MOD_VERSION":
				_serverHasResponded = true;
				if (!(MOD_VERSION == text2))
				{
					if (OLD_MOD_VERSIONS.Contains(text2))
					{
						_addServerModVersionOutOfDateMessage = true;
					}
					else
					{
						_askForKick = true;
					}
				}
				return;
			case "oomtm450_stats_kick":
				if (!(text2 != "1"))
				{
					if (!_sentOutOfDateMessage.TryGetValue(clientId, out var value))
					{
						value = DateTime.MinValue;
						_sentOutOfDateMessage.Add(clientId, value);
					}
					DateTime utcNow = DateTime.UtcNow;
					if (value + TimeSpan.FromSeconds(900.0) < utcNow && !string.IsNullOrEmpty(((object)NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId).Username.Value/*cast due to .constrained prefix*/).ToString()))
					{
						Logging.Log($"Warning client {clientId} mod out of date.", ServerConfig);
						NetworkBehaviourSingleton<UIChat>.Instance.Server_SendSystemChatMessage(string.Format("{0} : {1} Mod is out of date. Please unsubscribe from {2} in the workshop and restart your game to update.", NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(clientId).Username.Value, "Stats", "Stats"));
						_sentOutOfDateMessage[clientId] = utcNow;
					}
				}
				return;
			case "oomtm450_statsASKDATA":
				if (text2 != "1")
				{
					return;
				}
				NetworkCommunication.SendData("oomtm450_stats_MOD_VERSION", MOD_VERSION, clientId, "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
				if (_sog.Count != 0)
				{
					string text3 = "";
					foreach (string item in new List<string>(_sog.Keys))
					{
						text3 = text3 + item + ";" + _sog[item] + ";";
					}
					text3 = text3.Remove(text3.Length - 1);
					NetworkCommunication.SendData("oomtm450_statsBATCHSOG", text3, clientId, "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
				}
				if (_savePerc.Count != 0)
				{
					string text4 = "";
					foreach (string item2 in new List<string>(_savePerc.Keys))
					{
						text4 = text4 + item2 + ";" + _savePerc[item2].ToString() + ";";
					}
					text4 = text4.Remove(text4.Length - 1);
					NetworkCommunication.SendData("oomtm450_statsBATCHSAVEPERC", text4, clientId, "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
				}
				{
					foreach (int item3 in new List<int>(_stars.Keys))
					{
						NetworkCommunication.SendData("oomtm450_statsSTAR", $"{_stars[item3]};{item3}", clientId, "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
					}
					return;
				}
			case "oomtm450_statsRESETALL":
				if (!(text2 != "1"))
				{
					Client_ResetSOG();
					Client_ResetSavePerc();
					Client_ResetPasses();
					Client_ResetBlocks();
					Client_ResetHits();
					Client_ResetTakeaways();
					Client_ResetTurnovers();
					Client_ResetStickSaves();
					Client_ResetPlusMinus();
				}
				return;
			case "oomtm450_statsBATCHSOG":
			{
				string[] array = text2.Split(';');
				string playerSteamId = "";
				for (int i = 0; i < array.Length; i++)
				{
					if (i % 2 == 0)
					{
						playerSteamId = array[i];
					}
					else
					{
						ReceiveData_SOG(playerSteamId, array[i]);
					}
				}
				return;
			}
			case "oomtm450_statsBATCHSAVEPERC":
			{
				string[] array2 = text2.Split(';');
				string playerSteamId2 = "";
				for (int j = 0; j < array2.Length; j++)
				{
					if (j % 2 == 0)
					{
						playerSteamId2 = array2[j];
					}
					else
					{
						ReceiveData_SavePerc(playerSteamId2, array2[j]);
					}
				}
				return;
			}
			case "oomtm450_statsSTAR":
			{
				string[] array3 = text2.Split(';');
				string playerSteamId3 = "";
				for (int k = 0; k < array3.Length; k++)
				{
					if (k % 2 == 0)
					{
						playerSteamId3 = array3[k];
					}
					else
					{
						ReceiveData_Star(playerSteamId3, array3[k]);
					}
				}
				return;
			}
			}
			if (text.StartsWith("oomtm450_statsSOG"))
			{
				string text5 = text.Replace("oomtm450_statsSOG", "");
				if (string.IsNullOrEmpty(text5))
				{
					return;
				}
				ReceiveData_SOG(text5, text2);
			}
			if (text.StartsWith("oomtm450_statsSAVEPERC"))
			{
				string text6 = text.Replace("oomtm450_statsSAVEPERC", "");
				if (!string.IsNullOrEmpty(text6))
				{
					ReceiveData_SavePerc(text6, text2);
				}
			}
		}
		catch (Exception arg)
		{
			Logging.LogError($"Error in ReceiveData.\n{arg}", ServerConfig);
		}
	}

	private static void ReceiveData_SOG(string playerSteamId, string dataStr)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		int value = int.Parse(dataStr);
		if (_sog.TryGetValue(playerSteamId, out var _))
		{
			_sog[playerSteamId] = value;
			Player playerBySteamId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(FixedString32Bytes.op_Implicit(playerSteamId));
			if ((Object)(object)playerBySteamId != (Object)null && Object.op_Implicit((Object)(object)playerBySteamId) && !PlayerFunc.IsGoalie(playerBySteamId))
			{
				((TextElement)_sogLabels[playerSteamId]).text = value.ToString();
			}
		}
		else
		{
			_sog.Add(playerSteamId, value);
		}
		WriteClientSideFile_SOG();
	}

	private static void WriteClientSideFile_SOG()
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if (!_clientConfig.LogClientSideStats)
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<string, int> item in _sog)
		{
			Player playerBySteamId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(FixedString32Bytes.op_Implicit(item.Key));
			if (Object.op_Implicit((Object)(object)playerBySteamId) && !PlayerFunc.IsGoalie(playerBySteamId))
			{
				stringBuilder.AppendLine($"{playerBySteamId.Username.Value};{playerBySteamId.Number.Value};{playerBySteamId.Team.Value};{item.Key};{item.Value}");
			}
		}
		File.WriteAllText(Path.Combine(Path.GetFullPath("."), "oomtm450_stats_shots.csv"), stringBuilder.ToString());
	}

	private static void ReceiveData_SavePerc(string playerSteamId, string dataStr)
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		string[] array = SystemFunc.RemoveWhitespace(dataStr.Replace("(", "").Replace(")", "")).Split(',');
		int num = int.Parse(array[0]);
		int num2 = int.Parse(array[1]);
		if (_savePerc.TryGetValue(playerSteamId, out (int, int) _))
		{
			_savePerc[playerSteamId] = (num, num2);
			Player playerBySteamId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(FixedString32Bytes.op_Implicit(playerSteamId));
			if (Object.op_Implicit((Object)(object)playerBySteamId) && PlayerFunc.IsGoalie(playerBySteamId))
			{
				((TextElement)_sogLabels[playerSteamId]).text = GetGoalieSavePerc(num, num2);
			}
		}
		else
		{
			_savePerc.Add(playerSteamId, (num, num2));
		}
		WriteClientSideFile_SavePerc();
	}

	private static void WriteClientSideFile_SavePerc()
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		if (!_clientConfig.LogClientSideStats)
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<string, (int, int)> item in _savePerc)
		{
			Player playerBySteamId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(FixedString32Bytes.op_Implicit(item.Key));
			if (Object.op_Implicit((Object)(object)playerBySteamId) && PlayerFunc.IsGoalie(playerBySteamId))
			{
				stringBuilder.AppendLine($"{playerBySteamId.Username.Value};{playerBySteamId.Number.Value};{playerBySteamId.Team.Value};{item.Key};{item.Value.Item1};{item.Value.Item2}");
			}
		}
		File.WriteAllText(Path.Combine(Path.GetFullPath("."), "oomtm450_stats_saves.csv"), stringBuilder.ToString());
	}

	private static void ReceiveData_Star(string playerSteamId, string dataStr)
	{
		int num = int.Parse(dataStr);
		if (_stars.TryGetValue(num, out var _))
		{
			_stars[num] = playerSteamId;
		}
		else
		{
			_stars.Add(num, playerSteamId);
		}
	}

	private static void ScoreboardModifications(bool enable)
	{
		//IL_0356: Unknown result type (might be due to invalid IL or missing references)
		//IL_035b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Expected O, but got Unknown
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c8: Expected O, but got Unknown
		//IL_03d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0406: Unknown result type (might be due to invalid IL or missing references)
		//IL_041d: Unknown result type (might be due to invalid IL or missing references)
		//IL_042e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0438: Unknown result type (might be due to invalid IL or missing references)
		//IL_0674: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_04af: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02af: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_060d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0624: Unknown result type (might be due to invalid IL or missing references)
		//IL_0635: Unknown result type (might be due to invalid IL or missing references)
		//IL_063f: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)NetworkBehaviourSingleton<UIScoreboard>.Instance == (Object)null)
		{
			return;
		}
		VisualElement privateField = SystemFunc.GetPrivateField<VisualElement>(typeof(UIScoreboard), NetworkBehaviourSingleton<UIScoreboard>.Instance, "container");
		if (!_hasUpdatedUIScoreboard.Contains("header") && enable)
		{
			foreach (VisualElement item in privateField.Children())
			{
				if (!(item is TemplateContainer) || item.childCount != 1)
				{
					continue;
				}
				VisualElement obj = item.Children().First();
				Label val = new Label("S/SV%")
				{
					name = "SOGHeaderLabel"
				};
				obj.Add((VisualElement)(object)val);
				((VisualElement)val).transform.position = new Vector3(((VisualElement)val).transform.position.x - 260f, ((VisualElement)val).transform.position.y + 15f, ((VisualElement)val).transform.position.z);
				foreach (VisualElement item2 in obj.Children())
				{
					if (item2.name == "GoalsLabel" || item2.name == "AssistsLabel" || item2.name == "PointsLabel")
					{
						item2.transform.position = new Vector3(item2.transform.position.x - 100f, item2.transform.position.y, item2.transform.position.z);
					}
				}
			}
			_hasUpdatedUIScoreboard.Add("header");
		}
		else if (_hasUpdatedUIScoreboard.Contains("header") && !enable)
		{
			foreach (VisualElement item3 in privateField.Children())
			{
				if (!(item3 is TemplateContainer) || item3.childCount != 1)
				{
					continue;
				}
				VisualElement obj2 = item3.Children().First();
				obj2.Remove(obj2.Children().First((VisualElement x) => x.name == "SOGHeaderLabel"));
				foreach (VisualElement item4 in obj2.Children())
				{
					if (item4.name == "GoalsLabel" || item4.name == "AssistsLabel" || item4.name == "PointsLabel")
					{
						item4.transform.position = new Vector3(item4.transform.position.x + 100f, item4.transform.position.y, item4.transform.position.z);
					}
				}
			}
		}
		foreach (KeyValuePair<Player, VisualElement> item5 in SystemFunc.GetPrivateField<Dictionary<Player, VisualElement>>(typeof(UIScoreboard), NetworkBehaviourSingleton<UIScoreboard>.Instance, "playerVisualElementMap"))
		{
			string text = ((object)item5.Key.SteamId.Value/*cast due to .constrained prefix*/).ToString();
			if (string.IsNullOrEmpty(text) || !(!_hasUpdatedUIScoreboard.Contains(text) && enable))
			{
				continue;
			}
			if (item5.Value.childCount == 1)
			{
				VisualElement obj3 = item5.Value.Children().First();
				Label val2 = new Label("0")
				{
					name = "SOGLabel"
				};
				((VisualElement)val2).style.flexGrow = StyleFloat.op_Implicit(1f);
				((VisualElement)val2).style.unityTextAlign = StyleEnum<TextAnchor>.op_Implicit((TextAnchor)2);
				obj3.Add((VisualElement)(object)val2);
				((VisualElement)val2).transform.position = new Vector3(((VisualElement)val2).transform.position.x - 225f, ((VisualElement)val2).transform.position.y, ((VisualElement)val2).transform.position.z);
				_sogLabels.Add(text, val2);
				foreach (VisualElement item6 in obj3.Children())
				{
					if (item6.name == "GoalsLabel" || item6.name == "AssistsLabel" || item6.name == "PointsLabel")
					{
						item6.transform.position = new Vector3(item6.transform.position.x - 100f, item6.transform.position.y, item6.transform.position.z);
					}
				}
				_hasUpdatedUIScoreboard.Add(text);
				if (!_sog.TryGetValue(text, out var _))
				{
					_sog.Add(text, 0);
				}
				if (!_savePerc.TryGetValue(text, out (int, int) _))
				{
					_savePerc.Add(text, (0, 0));
				}
				continue;
			}
			if (_hasUpdatedUIScoreboard.Contains(text) && !enable)
			{
				VisualElement obj4 = item5.Value.Children().First();
				obj4.Remove(obj4.Children().First((VisualElement x) => x.name == "SOGLabel"));
				foreach (VisualElement item7 in obj4.Children())
				{
					if (item7.name == "GoalsLabel" || item7.name == "AssistsLabel" || item7.name == "PointsLabel")
					{
						item7.transform.position = new Vector3(item7.transform.position.x + 100f, item7.transform.position.y, item7.transform.position.z);
					}
				}
				continue;
			}
			Logging.Log($"Not adding player {item5.Key.Username.Value}, childCount {item5.Value.childCount}.", _clientConfig, bypassConfig: true);
			foreach (VisualElement item8 in item5.Value.Children())
			{
				Logging.Log(item8.name ?? "", _clientConfig, bypassConfig: true);
			}
		}
		if (!enable)
		{
			_sog.Clear();
			_savePerc.Clear();
			_sogLabels.Clear();
			_hasUpdatedUIScoreboard.Clear();
		}
	}

	private static bool SendSOGDuringGoal(Player player)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		ResetPuckWasSavedOrBlockedChecks();
		if (!_lastShotWasCounted[player.Team.Value])
		{
			string text = ((object)player.SteamId.Value/*cast due to .constrained prefix*/).ToString();
			if (string.IsNullOrEmpty(text))
			{
				return true;
			}
			if (!_sog.TryGetValue(text, out var _))
			{
				_sog.Add(text, 0);
			}
			_sog[text]++;
			int sog = _sog[text];
			NetworkCommunication.SendDataToAll("oomtm450_statsSOG" + text, sog.ToString(), "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
			LogSOG(text, sog);
			_lastShotWasCounted[player.Team.Value] = true;
			return false;
		}
		return true;
	}

	private static void ResetPuckWasSavedOrBlockedChecks()
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		foreach (PlayerTeam item in new List<PlayerTeam>(_checkIfPuckWasSaved.Keys))
		{
			_checkIfPuckWasSaved[item] = new SaveCheck();
		}
		foreach (PlayerTeam item2 in new List<PlayerTeam>(_checkIfPuckWasBlocked.Keys))
		{
			_checkIfPuckWasBlocked[item2] = new BlockCheck();
		}
	}

	private static void SendSavePercDuringGoal(PlayerTeam team, bool saveWasCounted)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		Player otherTeamGoalie = PlayerFunc.GetOtherTeamGoalie(team);
		if (!((Object)(object)otherTeamGoalie == (Object)null))
		{
			string text = ((object)otherTeamGoalie.SteamId.Value/*cast due to .constrained prefix*/).ToString();
			if (!_savePerc.TryGetValue(text, out (int, int) value))
			{
				_savePerc.Add(text, (0, 0));
				value = (0, 0);
			}
			var (saves, sog) = (_savePerc[text] = (saveWasCounted ? (--value.Item1, value.Item2) : (value.Item1, ++value.Item2)));
			NetworkCommunication.SendDataToAll("oomtm450_statsSAVEPERC" + text, _savePerc[text].ToString(), "oomtm450_stats_server", ServerConfig, (NetworkDelivery)4);
			LogSavePerc(text, saves, sog);
		}
	}

	private static void LogSavePerc(string goaliePlayerSteamId, int saves, int sog)
	{
		Logging.Log($"playerSteamId:{goaliePlayerSteamId},saveperc:{GetGoalieSavePerc(saves, sog)},saves:{saves},sog:{sog}", ServerConfig);
	}

	private static void LogStickSave(string playerSteamId, int stickSaves)
	{
		Logging.Log($"playerSteamId:{playerSteamId},sticksv:{stickSaves}", ServerConfig);
	}

	private static void LogSOG(string playerSteamId, int sog)
	{
		Logging.Log($"playerSteamId:{playerSteamId},sog:{sog}", ServerConfig);
	}

	private static void LogBlock(string playerSteamId, int block)
	{
		Logging.Log($"playerSteamId:{playerSteamId},block:{block}", ServerConfig);
	}

	private static void LogHit(string playerSteamId, int hit)
	{
		Logging.Log($"playerSteamId:{playerSteamId},hit:{hit}", ServerConfig);
	}

	private static void LogTakeaways(string playerSteamId, int takeaway)
	{
		Logging.Log($"playerSteamId:{playerSteamId},takeaway:{takeaway}", ServerConfig);
	}

	private static void LogTurnovers(string playerSteamId, int turnover)
	{
		Logging.Log($"playerSteamId:{playerSteamId},turnover:{turnover}", ServerConfig);
	}

	private static void LogPass(string playerSteamId, int pass)
	{
		Logging.Log($"playerSteamId:{playerSteamId},pass:{pass}", ServerConfig);
	}

	private static void LogGWG(string playerSteamId)
	{
		Logging.Log("playerSteamId:" + playerSteamId + ",gwg:1", ServerConfig);
	}

	private static void LogStar(string playerSteamId, int starIndex)
	{
		Logging.Log($"playerSteamId:{playerSteamId},star:{starIndex}", ServerConfig);
	}

	private static void LogPlusMinus(string playerSteamId, int plusminus)
	{
		Logging.Log($"playerSteamId:{playerSteamId},plusminus:{plusminus}", ServerConfig);
	}

	private static string GetGoalieSavePerc(int saves, int shots)
	{
		if (shots == 0)
		{
			return "0.000";
		}
		return ((double)saves / (double)shots).ToString("0.000", CultureInfo.InvariantCulture);
	}

	private static bool RulesetModEnabled()
	{
		if (_rulesetModEnabled.HasValue)
		{
			return _rulesetModEnabled.Value;
		}
		return false;
	}

	private static string GetStarTag(string playerSteamId)
	{
		string result = "";
		if (_stars[1] == playerSteamId)
		{
			result = "<color=#FFD700FF><b>★</b></color> ";
		}
		else if (_stars[2] == playerSteamId)
		{
			result = "<color=#C0C0C0FF><b>★</b></color> ";
		}
		else if (_stars[3] == playerSteamId)
		{
			result = "<color=#CD7F32FF><b>★</b></color> ";
		}
		return result;
	}

	private static void Client_ResetSOG()
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		foreach (string item in new List<string>(_sog.Keys))
		{
			if (_sogLabels.TryGetValue(item, out var value))
			{
				_sog[item] = 0;
				((TextElement)value).text = "0";
				Player playerBySteamId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerBySteamId(FixedString32Bytes.op_Implicit(item));
				if ((Object)(object)playerBySteamId != (Object)null && Object.op_Implicit((Object)(object)playerBySteamId) && PlayerFunc.IsGoalie(playerBySteamId))
				{
					((TextElement)value).text = "0.000";
				}
			}
			else
			{
				_sog.Remove(item);
				_savePerc.Remove(item);
			}
		}
		WriteClientSideFile_SOG();
	}

	private static void Client_ResetSavePerc()
	{
		foreach (string item in new List<string>(_savePerc.Keys))
		{
			_savePerc[item] = (0, 0);
		}
		WriteClientSideFile_SavePerc();
	}

	private static void Client_ResetPasses()
	{
		foreach (string item in new List<string>(_passes.Keys))
		{
			_passes[item] = 0;
		}
	}

	private static void Client_ResetBlocks()
	{
		foreach (string item in new List<string>(_blocks.Keys))
		{
			_blocks[item] = 0;
		}
	}

	private static void Client_ResetHits()
	{
		foreach (string item in new List<string>(_hits.Keys))
		{
			_hits[item] = 0;
		}
	}

	private static void Client_ResetTakeaways()
	{
		foreach (string item in new List<string>(_takeaways.Keys))
		{
			_takeaways[item] = 0;
		}
	}

	private static void Client_ResetTurnovers()
	{
		foreach (string item in new List<string>(_turnovers.Keys))
		{
			_turnovers[item] = 0;
		}
	}

	private static void Client_ResetStickSaves()
	{
		foreach (string item in new List<string>(_stickSaves.Keys))
		{
			_stickSaves[item] = 0;
		}
	}

	private static void Client_ResetPlusMinus()
	{
		foreach (string item in new List<string>(_plusMinus.Keys))
		{
			_plusMinus[item] = 0;
		}
	}
}
