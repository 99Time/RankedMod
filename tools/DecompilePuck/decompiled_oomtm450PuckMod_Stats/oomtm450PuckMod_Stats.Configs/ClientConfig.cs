using System;
using System.IO;
using Codebase;
using Codebase.Configs;
using Newtonsoft.Json;

namespace oomtm450PuckMod_Stats.Configs;

public class ClientConfig : IConfig
{
	[JsonIgnore]
	private readonly string _configPath = Path.Combine(Path.GetFullPath("."), "oomtm450_stats_clientconfig.json");

	public bool LogInfo { get; set; } = true;

	public bool LogClientSideStats { get; set; }

	[JsonIgnore]
	public string ModName { get; } = "oomtm450_stats";

	public override string ToString()
	{
		return JsonConvert.SerializeObject((object)this, (Formatting)1);
	}

	internal static ClientConfig SetConfig(string json)
	{
		return JsonConvert.DeserializeObject<ClientConfig>(json);
	}

	internal static ClientConfig ReadConfig()
	{
		ClientConfig clientConfig = new ClientConfig();
		try
		{
			if (File.Exists(clientConfig._configPath))
			{
				clientConfig = SetConfig(File.ReadAllText(clientConfig._configPath));
				Logging.Log("Client config read.", clientConfig, bypassConfig: true);
			}
			clientConfig.Save();
		}
		catch (Exception arg)
		{
			Logging.LogError($"Can't read the server config file/folder. (Permission error ?)\n{arg}", clientConfig);
		}
		return clientConfig;
	}

	internal void Save()
	{
		if (string.IsNullOrEmpty(_configPath))
		{
			Logging.LogError("Can't write the client config file. (_configPath null or empty)", this);
			return;
		}
		try
		{
			File.WriteAllText(_configPath, ToString());
		}
		catch (Exception arg)
		{
			Logging.LogError($"Can't write the client config file. (Permission error ?)\n{arg}", this);
		}
		Logging.Log("Wrote client config : " + ToString(), this, bypassConfig: true);
	}
}
