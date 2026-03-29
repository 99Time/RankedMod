using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using PuckAIPractice.Singletons;
using UnityEngine;

namespace PuckAIPractice.Config;

public static class ModConfig
{
	public static string ConfigPath { get; private set; }

	public static void Initialize()
	{
		try
		{
			string location = Assembly.GetExecutingAssembly().Location;
			Debug.Log((object)("[PuckAIPractice] Mod assembly path: " + location));
			Debug.Log((object)"-----");
			string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(location), "..\\..\\..\\.."));
			string text = Path.Combine(fullPath, "common", "Puck");
			Debug.Log((object)("[PuckAIPractice] Resolved gameDir: " + text));
			string text2 = Path.Combine(text, "config");
			if (!Directory.Exists(text2))
			{
				Debug.Log((object)"[PuckAIPractice] Creating config directory...");
				Directory.CreateDirectory(text2);
			}
			ConfigPath = Path.Combine(text2, "PuckAIPracticeConfig.json");
			Debug.Log((object)("[PuckAIPractice] Final ConfigPath: " + ConfigPath));
			string text3 = Path.Combine(Path.GetDirectoryName(location), "PuckAIPracticeConfig.json");
			Debug.Log((object)("[PuckAIPractice] Looking for default config at: " + text3));
			if (!File.Exists(ConfigPath))
			{
				if (File.Exists(text3))
				{
					Debug.Log((object)"[PuckAIPractice] Copying default config to game config folder...");
					File.Copy(text3, ConfigPath);
				}
				else
				{
					Debug.LogWarning((object)"[PuckAIPractice] No default config found, generating new one...");
					string contents = JsonConvert.SerializeObject((object)new ConfigData(), (Formatting)1);
					File.WriteAllText(ConfigPath, contents);
				}
			}
		}
		catch (Exception arg)
		{
			Debug.LogError((object)$"[PuckAIPractice] Error during ModConfig.Initialize: {arg}");
		}
	}
}
