using System;
using System.IO;
using Codebase;
using Codebase.Configs;
using Newtonsoft.Json;

namespace oomtm450PuckMod_Stats.Configs;

public class ServerConfig : IConfig, ISubConfig
{
	public const string CONFIG_DATA_NAME = "oomtm450_stats_config";

	public bool LogInfo { get; set; } = true;

	public bool UseDefaultNumericValues { get; set; } = true;

	public float GoalieRadius { get; set; } = 0.75f;

	public float GoalieSaveCreaseSystemZDelta { get; set; } = 0.0125f;

	public bool SaveEOGJSON { get; set; } = true;

	public int MaxTippedMilliseconds { get; set; } = 91;

	public int MinPossessionMilliseconds { get; set; } = 450;

	public int MaxPossessionMilliseconds { get; set; } = 1000;

	public int TurnoverThresholdMilliseconds { get; set; } = 500;

	[JsonIgnore]
	public string ModName { get; } = "oomtm450_stats";

	public void UpdateDefaultValues(ISubConfig oldConfig)
	{
		if (!(oldConfig is OldServerConfig))
		{
			throw new ArgumentException("oldConfig has to be typeof OldServerConfig.", "oldConfig");
		}
		OldServerConfig oldServerConfig = oldConfig as OldServerConfig;
		ServerConfig serverConfig = new ServerConfig();
		if (GoalieRadius == oldServerConfig.GoalieRadius)
		{
			GoalieRadius = serverConfig.GoalieRadius;
		}
		if (GoalieSaveCreaseSystemZDelta == oldServerConfig.GoalieSaveCreaseSystemZDelta)
		{
			GoalieSaveCreaseSystemZDelta = serverConfig.GoalieSaveCreaseSystemZDelta;
		}
		if (SaveEOGJSON == oldServerConfig.SaveEOGJSON)
		{
			SaveEOGJSON = serverConfig.SaveEOGJSON;
		}
		if (MaxTippedMilliseconds == oldServerConfig.MaxTippedMilliseconds)
		{
			MaxTippedMilliseconds = serverConfig.MaxTippedMilliseconds;
		}
		if (MinPossessionMilliseconds == oldServerConfig.MinPossessionMilliseconds)
		{
			MinPossessionMilliseconds = serverConfig.MinPossessionMilliseconds;
		}
		if (MaxPossessionMilliseconds == oldServerConfig.MaxPossessionMilliseconds)
		{
			MaxPossessionMilliseconds = serverConfig.MaxPossessionMilliseconds;
		}
		if (TurnoverThresholdMilliseconds == oldServerConfig.TurnoverThresholdMilliseconds)
		{
			TurnoverThresholdMilliseconds = serverConfig.TurnoverThresholdMilliseconds;
		}
	}

	public override string ToString()
	{
		return JsonConvert.SerializeObject((object)this, (Formatting)1);
	}

	internal static ServerConfig SetConfig(string json)
	{
		return JsonConvert.DeserializeObject<ServerConfig>(json);
	}

	internal static ServerConfig ReadConfig()
	{
		ServerConfig serverConfig = new ServerConfig();
		try
		{
			string path = Path.Combine(Path.GetFullPath("."), "oomtm450_stats_serverconfig.json");
			if (File.Exists(path))
			{
				serverConfig = SetConfig(File.ReadAllText(path));
				Logging.Log("Server config read.", serverConfig, bypassConfig: true);
			}
			serverConfig.UpdateDefaultValues(new OldServerConfig());
			try
			{
				File.WriteAllText(path, serverConfig.ToString());
			}
			catch (Exception arg)
			{
				Logging.LogError($"Can't write the server config file. (Permission error ?)\n{arg}", serverConfig);
			}
			Logging.Log($"Wrote server config : {serverConfig}", serverConfig, bypassConfig: true);
			if (serverConfig.UseDefaultNumericValues)
			{
				serverConfig = new ServerConfig
				{
					LogInfo = serverConfig.LogInfo,
					UseDefaultNumericValues = serverConfig.UseDefaultNumericValues,
					SaveEOGJSON = serverConfig.SaveEOGJSON
				};
			}
		}
		catch (Exception arg2)
		{
			Logging.LogError($"Can't read the server config file/folder. (Permission error ?)\n{arg2}", serverConfig);
		}
		return serverConfig;
	}
}
