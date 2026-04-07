using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Unity.Netcode;
using UnityEngine;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETFramework,Version=v4.7.2", FrameworkDisplayName = ".NET Framework 4.7.2")]
[assembly: AssemblyCompany("PonceGoalieAutoSwitcher")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
[assembly: AssemblyProduct("PonceGoalieAutoSwitcher")]
[assembly: AssemblyTitle("PonceGoalieAutoSwitcher")]
[assembly: AssemblyVersion("1.0.0.0")]
[module: RefSafetyRules(11)]
namespace Microsoft.CodeAnalysis
{
	[CompilerGenerated]
	[Embedded]
	internal sealed class EmbeddedAttribute : Attribute
	{
	}
}
namespace System.Runtime.CompilerServices
{
	[CompilerGenerated]
	[Embedded]
	[AttributeUsage(AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
	internal sealed class RefSafetyRulesAttribute : Attribute
	{
		public readonly int Version;

		public RefSafetyRulesAttribute(int P_0)
		{
			Version = P_0;
		}
	}
}
namespace GoalieAutoSwitcher
{
	public class Class1 : IPuckMod
	{
		[HarmonyPatch(typeof(Puck), "FixedUpdate")]
		public class CheckPuckPosition
		{
			private const float Z_THRESHOLD = 2f;

			private static readonly PlayerRole ROLE_GOALIE = (PlayerRole)2;

			private static readonly PlayerTeam TEAM_2 = (PlayerTeam)2;

			private static readonly PlayerTeam TEAM_3 = (PlayerTeam)3;

			private static bool _autoActive;

			[HarmonyPrefix]
			public static void Prefix(Puck __instance)
			{
				//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
				//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
				//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
				//IL_0106: Unknown result type (might be due to invalid IL or missing references)
				//IL_010b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0119: Unknown result type (might be due to invalid IL or missing references)
				//IL_011f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0155: Unknown result type (might be due to invalid IL or missing references)
				//IL_0194: Unknown result type (might be due to invalid IL or missing references)
				if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
				{
					return;
				}
				List<Player> list = NetworkBehaviourSingleton<PlayerManager>.Instance.GetPlayers(false).FindAll((Player p) => p.Team.Value == TEAM_2 || p.Team.Value == TEAM_3).FindAll((Player p) => p.Role.Value == ROLE_GOALIE);
				if (list.Count != 1)
				{
					if (_autoActive)
					{
						_autoActive = false;
						Debug.Log((object)$"[GoalieAutoSwitcher] Disabled (goalies={list.Count}).");
					}
					return;
				}
				if (!_autoActive)
				{
					_autoActive = true;
					Debug.Log((object)"[GoalieAutoSwitcher] Enabled (exactly one goalie active).");
				}
				if (NetworkBehaviourSingleton<PuckManager>.Instance.GetPucks(false).Count != 1)
				{
					return;
				}
				Player val = list[0];
				float z = ((Component)__instance).transform.position.z;
				PlayerTeam intendedTeam;
				if (z > 2f)
				{
					intendedTeam = TEAM_2;
				}
				else
				{
					if (!(z < -2f))
					{
						return;
					}
					intendedTeam = TEAM_3;
				}
				if (val.Team.Value != intendedTeam)
				{
					ServerConfigurationManager serverConfigurationManager = NetworkBehaviourSingleton<ServerManager>.Instance.ServerConfigurationManager;
					float joinMidMatchDelay = serverConfigurationManager.ServerConfiguration.joinMidMatchDelay;
					serverConfigurationManager.ServerConfiguration.joinMidMatchDelay = 0f;
					val.Team.Value = intendedTeam;
					PlayerPosition val2 = NetworkBehaviourSingleton<PlayerPositionManager>.Instance.AllPositions.Find((PlayerPosition pos) => pos.Role == ROLE_GOALIE && pos.Team == intendedTeam);
					if ((Object)(object)val2 != (Object)null)
					{
						val2.Server_Claim(val);
						Debug.Log((object)$"[GoalieAutoSwitcher] Moved lone goalie to Team {intendedTeam} (puck z={z:0.00}).");
					}
					else
					{
						Debug.LogWarning((object)"[GoalieAutoSwitcher] No goalie position on intended team; switch aborted.");
					}
					serverConfigurationManager.ServerConfiguration.joinMidMatchDelay = joinMidMatchDelay;
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
				Debug.LogError((object)("[GoalieAutoSwitcher] Harmony patch failed: " + ex.Message));
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
				Debug.LogError((object)("[GoalieAutoSwitcher] Harmony unpatch failed: " + ex.Message));
				return false;
			}
			return true;
		}
	}
}
