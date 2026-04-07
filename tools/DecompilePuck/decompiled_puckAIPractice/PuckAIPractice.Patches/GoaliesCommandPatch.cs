using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PuckAIPractice.AI;
using PuckAIPractice.GameModes;
using PuckAIPractice.Utilities;
using Unity.Collections;
using UnityEngine;

namespace PuckAIPractice.Patches;

[HarmonyPatch(typeof(VoteManagerController), "Event_Server_OnChatCommand")]
public static class GoaliesCommandPatch
{
	[HarmonyPrefix]
	public static bool Prefix(VoteManagerController __instance, Dictionary<string, object> message)
	{
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Expected O, but got Unknown
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		if (!PracticeModeDetector.IsPracticeMode)
		{
			return true;
		}
		string text = (string)message["command"];
		ulong num = (ulong)message["clientId"];
		string[] array = (string[])message["args"];
		Player playerByClientId = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(num);
		VoteChatCommandHelper.VotesNeeded = Mathf.RoundToInt((float)NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers(false).Count / 2f + 0.5f);
		if (text == "/chaser" && array.Count() == 0)
		{
			BotSpawning.SpawnChaser((PlayerTeam)2, (PlayerRole)1);
		}
		else if (text == "/goalies" && array.Count() == 1)
		{
			VoteManager val = (VoteManager)Traverse.Create((object)__instance).Field("voteManager").GetValue();
			Traverse val2 = Traverse.Create((object)val);
			Player playerByUsername = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByUsername(FixedString32Bytes.op_Implicit("dem"), false);
			List<string> list = new List<string>
			{
				"both",
				array[0]
			};
			Player playerByClientId2 = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(num);
			if ((Object)(object)playerByClientId2 != (Object)null)
			{
				return SpawnGoaliesBasedOffCommand(array[0], GoalieSession.Both);
			}
		}
		else if (text == "/goalie" && array.Count() == 2)
		{
			Player playerByClientId3 = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayerByClientId(num);
			if ((Object)(object)playerByClientId3 != (Object)null)
			{
				GoalieSession goalieSession = GoalieSession.Red;
				if (array[1].ToLower() == "red")
				{
					goalieSession = GoalieSession.Red;
				}
				else
				{
					if (!(array[1].ToLower() == "blue"))
					{
						return true;
					}
					goalieSession = GoalieSession.Blue;
				}
				return SpawnGoaliesBasedOffCommand(array[0], goalieSession);
			}
		}
		else
		{
			if (!(text == "/endgoaliesession"))
			{
				return true;
			}
			Goalies.EndGoalieSession(GoalieSession.Both);
		}
		return false;
	}

	public static void ApplyGoalieSettings(string difficulty, GoalieSession type)
	{
		if (difficulty.ToLower() == "easy")
		{
			switch (type)
			{
			case GoalieSession.Red:
				GoalieSettings.InstanceRed.ApplyDifficulty(GoalieDifficulty.Easy);
				break;
			case GoalieSession.Blue:
				GoalieSettings.InstanceBlue.ApplyDifficulty(GoalieDifficulty.Easy);
				break;
			default:
				GoalieSettings.InstanceRed.ApplyDifficulty(GoalieDifficulty.Easy);
				GoalieSettings.InstanceBlue.ApplyDifficulty(GoalieDifficulty.Easy);
				break;
			}
		}
		else if (difficulty.ToLower() == "normal")
		{
			switch (type)
			{
			case GoalieSession.Red:
				GoalieSettings.InstanceRed.ApplyDifficulty(GoalieDifficulty.Normal);
				break;
			case GoalieSession.Blue:
				GoalieSettings.InstanceBlue.ApplyDifficulty(GoalieDifficulty.Normal);
				break;
			default:
				GoalieSettings.InstanceRed.ApplyDifficulty(GoalieDifficulty.Normal);
				GoalieSettings.InstanceBlue.ApplyDifficulty(GoalieDifficulty.Normal);
				break;
			}
		}
		else if (difficulty.ToLower() == "hard")
		{
			switch (type)
			{
			case GoalieSession.Red:
				GoalieSettings.InstanceRed.ApplyDifficulty(GoalieDifficulty.Hard);
				break;
			case GoalieSession.Blue:
				GoalieSettings.InstanceBlue.ApplyDifficulty(GoalieDifficulty.Hard);
				break;
			default:
				GoalieSettings.InstanceRed.ApplyDifficulty(GoalieDifficulty.Hard);
				GoalieSettings.InstanceBlue.ApplyDifficulty(GoalieDifficulty.Hard);
				break;
			}
		}
	}

	public static bool SpawnGoaliesBasedOffCommand(string difficulty, GoalieSession type)
	{
		if (!Goalies.GoaliesAreRunning)
		{
			ApplyGoalieSettings(difficulty, type);
			if (difficulty.ToLower() == "easy")
			{
				Goalies.GoaliesAreRunning = true;
			}
			else if (difficulty.ToLower() == "normal")
			{
				Goalies.GoaliesAreRunning = true;
			}
			else
			{
				if (!(difficulty.ToLower() == "hard"))
				{
					return true;
				}
				Goalies.GoaliesAreRunning = true;
			}
		}
		else if (difficulty.ToLower() != "end")
		{
			Goalies.GoaliesAreRunning = false;
			ApplyGoalieSettings(difficulty, type);
			if (difficulty.ToLower() == "easy")
			{
				Goalies.EndGoalieSession(type);
				Goalies.GoaliesAreRunning = true;
			}
			else if (difficulty.ToLower() == "normal")
			{
				Goalies.EndGoalieSession(type);
			}
			else
			{
				if (!(difficulty.ToLower() == "hard"))
				{
					return true;
				}
				Goalies.EndGoalieSession(type);
				Goalies.GoaliesAreRunning = true;
			}
		}
		return false;
	}
}
