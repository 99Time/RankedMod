using System;
using HarmonyLib;
using PuckAIPractice.AI;
using PuckAIPractice.Config;
using PuckAIPractice.GameModes;
using PuckAIPractice.Singletons;
using PuckAIPractice.Utilities;
using UnityEngine;

namespace PuckAIPractice;

public class InitializePuckAI : IPuckMod
{
	private static readonly Harmony harmony = new Harmony("GAFURIX.PuckAIPractice");

	public bool OnEnable()
	{
		Debug.Log((object)"[PuckAIPractice] Enabled");
		try
		{
			GoalieRunner.Initialize();
			ModConfig.Initialize();
			DetectPositions.Create();
			ConfigData.Load();
			Goalies.GoaliesAreRunning = true;
			GoalieSettings.InstanceBlue.ApplyDifficulty(ConfigData.Instance.BlueGoalieDefaultDifficulty);
			GoalieSettings.InstanceRed.ApplyDifficulty(ConfigData.Instance.RedGoalieDefaultDifficulty);
			harmony.PatchAll();
		}
		catch (Exception arg)
		{
			Debug.LogError((object)$"[PuckAIPractice] Harmony patch failed: {arg}");
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
		catch (Exception arg)
		{
			Debug.LogError((object)$"[PuckAIPractice] Harmony unpatch failed: {arg}");
			return false;
		}
		return true;
	}
}
