using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Codebase;

public class PlayerFunc
{
	public const string GOALIE_POSITION = "G";

	public const string LEFT_WINGER_POSITION = "LW";

	public const string CENTER_POSITION = "C";

	public const string RIGHT_WINGER_POSITION = "RW";

	public const string LEFT_DEFENDER_POSITION = "LD";

	public const string RIGHT_DEFENDER_POSITION = "RD";

	public static bool IsPlayerPlaying(Player player)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Invalid comparison between Unknown and I4
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Invalid comparison between Unknown and I4
		if (Object.op_Implicit((Object)(object)player) && (int)player.Role.Value != 0 && player.IsCharacterFullySpawned)
		{
			if ((int)player.Team.Value != 3)
			{
				return (int)player.Team.Value == 2;
			}
			return true;
		}
		return false;
	}

	public static Player GetTeamGoalie(PlayerTeam team)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayersByTeam(team, false).FirstOrDefault((Player x) => (int)x.Role.Value == 2);
	}

	public static Player GetOtherTeamGoalie(PlayerTeam team)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayersByTeam(TeamFunc.GetOtherTeam(team), false).FirstOrDefault((Player x) => (int)x.Role.Value == 2);
	}

	public static bool IsRole(PlayerPosition pPosition, PlayerRole role, bool hasToBeClaimed = true)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		bool flag = pPosition.Role == role;
		if (hasToBeClaimed)
		{
			if (flag)
			{
				return pPosition.IsClaimed;
			}
			return false;
		}
		return flag;
	}

	public static bool IsAttacker(PlayerPosition pPosition, bool hasToBeClaimed = true)
	{
		return IsRole(pPosition, (PlayerRole)1, hasToBeClaimed);
	}

	public static bool IsGoalie(PlayerPosition pPosition, bool hasToBeClaimed = true)
	{
		return IsRole(pPosition, (PlayerRole)2, hasToBeClaimed);
	}

	public static bool IsGoalie(Player player)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Invalid comparison between Unknown and I4
		return (int)player.Role.Value == 2;
	}

	public static string GetPlayerSteamIdInPossession(int minPossessionMilliseconds, LockDictionary<string, Stopwatch> playersCurrentPuckTouch, bool checkForChallenge = true)
	{
		Dictionary<string, Stopwatch> dictionary = playersCurrentPuckTouch.Where((KeyValuePair<string, Stopwatch> x) => x.Value.ElapsedMilliseconds > minPossessionMilliseconds).ToDictionary((KeyValuePair<string, Stopwatch> x) => x.Key, (KeyValuePair<string, Stopwatch> x) => x.Value);
		if (dictionary.Count > 1)
		{
			if (checkForChallenge)
			{
				return "";
			}
			return dictionary.OrderBy((KeyValuePair<string, Stopwatch> x) => x.Value.ElapsedMilliseconds).First().Key;
		}
		if (dictionary.Count == 1)
		{
			return dictionary.First().Key;
		}
		return "";
	}
}
