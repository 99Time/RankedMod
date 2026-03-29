using System;
using System.IO;
using Newtonsoft.Json;
using PuckAIPractice.AI;
using PuckAIPractice.Config;
using UnityEngine;

namespace PuckAIPractice.Singletons;

public class ConfigData
{
	private static ConfigData _instance;

	private static string ConfigPath => ModConfig.ConfigPath;

	public static ConfigData Instance
	{
		get
		{
			if (_instance == null)
			{
				Load();
			}
			return _instance;
		}
	}

	public bool StartWithBlueGoalie { get; set; } = false;

	public bool StartWithRedGoalie { get; set; } = false;

	public GoalieDifficulty RedGoalieDefaultDifficulty { get; set; } = GoalieDifficulty.Normal;

	public GoalieDifficulty BlueGoalieDefaultDifficulty { get; set; } = GoalieDifficulty.Normal;

	public bool IsServer { get; set; } = true;

	public static void Load()
	{
		try
		{
			Debug.Log((object)"[InputControl] Loading config...");
			if (!File.Exists(ConfigPath))
			{
				Debug.Log((object)("[InputControl] Config not found at " + ConfigPath + ", initializing..."));
				ModConfig.Initialize();
			}
			string text = File.ReadAllText(ConfigPath);
			Debug.Log((object)("[InputControl] Raw config contents: " + text));
			_instance = JsonConvert.DeserializeObject<ConfigData>(text);
			if (_instance == null)
			{
				Debug.LogWarning((object)"[InputControl] Config deserialized to null, using defaults.");
				_instance = new ConfigData();
			}
			Debug.Log((object)"[InputControl] Config loaded successfully.");
		}
		catch (Exception arg)
		{
			Debug.LogError((object)$"[InputControl] Failed to load config: {arg}");
			_instance = new ConfigData();
			Save();
		}
	}

	public static void Save()
	{
		try
		{
			if (_instance == null)
			{
				Debug.LogWarning((object)"[InputControl] Save called but _instance is null.");
				return;
			}
			string contents = JsonConvert.SerializeObject((object)_instance, (Formatting)1);
			File.WriteAllText(ConfigPath, contents);
			Debug.Log((object)("[InputControl] Saved config to " + ConfigPath + "."));
		}
		catch (Exception arg)
		{
			Debug.LogError((object)$"[InputControl] Failed to save config: {arg}");
		}
	}
}
