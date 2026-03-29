using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;

public class ServerConfigurationManager : MonoBehaviour
{
	public ServerConfiguration ServerConfiguration { get; set; }

	public ulong[] ClientRequiredModIds => (from mod in ServerConfiguration.mods
		where mod.clientRequired
		select mod.id).ToArray();

	public ulong[] EnabledModIds => (from mod in ServerConfiguration.mods
		where mod.enabled
		select mod.id).ToArray();

	private void Awake()
	{
		if (!Application.isBatchMode)
		{
			return;
		}
		string path = "./server_configuration.json";
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 0; i < commandLineArgs.Length; i++)
		{
			if (commandLineArgs[i] == "--serverConfigurationPath")
			{
				path = commandLineArgs[i + 1];
			}
		}
		string text = Uri.UnescapeDataString(new Uri(Path.GetFullPath(path)).AbsolutePath);
		string environmentVariable = Environment.GetEnvironmentVariable("PUCK_SERVER_CONFIGURATION");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			Debug.Log("[ServerConfigurationManager] Reading server configuration from environment variable PUCK_SERVER_CONFIGURATION...");
			string text2 = environmentVariable;
			Debug.Log("[ServerConfigurationManager] PUCK_SERVER_CONFIGURATION: " + text2);
			Debug.Log("[ServerConfigurationManager] Parsing server configuration...");
			ServerConfiguration = JsonSerializer.Deserialize<ServerConfiguration>(text2);
			return;
		}
		if (File.Exists(text))
		{
			Debug.Log("[ServerConfigurationManager] Reading server configuration file from " + text + "...");
		}
		else
		{
			Debug.Log("[ServerConfigurationManager] Server configuration file not found at " + text + ", creating...");
			File.AppendAllText(text, JsonSerializer.Serialize(new ServerConfiguration(), new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
		string text3 = File.ReadAllText(text);
		Debug.Log("[ServerConfigurationManager] " + text + ": " + text3);
		Debug.Log("[ServerConfigurationManager] Parsing server configuration...");
		ServerConfiguration = JsonSerializer.Deserialize<ServerConfiguration>(text3);
	}
}
