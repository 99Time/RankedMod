using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = "")]
[assembly: AssemblyCompany("GoalieAutoSwitcher")]
[assembly: AssemblyConfiguration("Debug")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0+df09be82845f5c9b5c1eea8f9c1de849425ebfd1")]
[assembly: AssemblyProduct("GoalieAutoSwitcher")]
[assembly: AssemblyTitle("GoalieAutoSwitcher")]
[assembly: AssemblyVersion("1.0.0.0")]
namespace GoalieAutoSwitcher;

public class Class1 : IPuckMod
{
	[HarmonyPatch(typeof(Puck), "FixedUpdate")]
	public class CheckPuckPosition
	{
		[HarmonyPrefix]
		public static void Postfix(Puck __instance)
		{
			//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0119: Unknown result type (might be due to invalid IL or missing references)
			//IL_012d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0133: Unknown result type (might be due to invalid IL or missing references)
			//IL_0179: Unknown result type (might be due to invalid IL or missing references)
			if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
			{
				return;
			}
			List<Player> list = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers(false).FindAll((Player player) => (int)player.Team.Value == 2 || (int)player.Team.Value == 3);
			List<Player> list2 = list.FindAll((Player player) => (int)player.Role.Value == 2);
			if (list.Count % 2 == 0 || list2.Count != 1 || NetworkBehaviourSingleton<PuckManager>.Instance.GetPucks(false).Count != 1)
			{
				return;
			}
			Player val = list2[0];
			PlayerTeam intendedTeam;
			if (((Component)__instance).transform.position.z > 2f)
			{
				intendedTeam = (PlayerTeam)2;
			}
			else
			{
				if (!(((Component)__instance).transform.position.z < -2f))
				{
					return;
				}
				intendedTeam = (PlayerTeam)3;
			}
			if (val.Team.Value != intendedTeam)
			{
				float joinMidMatchDelay = NetworkBehaviourSingleton<ServerManager>.Instance.ServerConfigurationManager.ServerConfiguration.joinMidMatchDelay;
				NetworkBehaviourSingleton<ServerManager>.Instance.ServerConfigurationManager.ServerConfiguration.joinMidMatchDelay = 0f;
				val.Team.Value = intendedTeam;
				PlayerPosition val2 = NetworkBehaviourSingleton<PlayerPositionManager>.Instance.AllPositions.Find((PlayerPosition position) => (int)position.Role == 2 && position.Team == intendedTeam);
				val2.Server_Claim(val);
				NetworkBehaviourSingleton<ServerManager>.Instance.ServerConfigurationManager.ServerConfiguration.joinMidMatchDelay = joinMidMatchDelay;
			}
		}
	}

	private static readonly Harmony harmony = new Harmony("wenright.GoalieAutoSwitcher");

	public bool OnEnable()
	{
		try
		{
			harmony.PatchAll();
		}
		catch (Exception ex)
		{
			Debug.LogError((object)("Harmony patch failed: " + ex.Message));
			return false;
		}
		return true;
	}

	public bool OnDisable()
	{
		try
		{
			harmony.UnpatchSelf();
		}
		catch (Exception ex)
		{
			Debug.LogError((object)("Harmony unpatch failed: " + ex.Message));
			return false;
		}
		return true;
	}
}
