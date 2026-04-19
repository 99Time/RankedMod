using System;
using Codebase.Configs;

namespace oomtm450PuckMod_Stats.Configs;

public class OldServerConfig : ISubConfig
{
	public bool LogInfo { get; } = true;

	public float GoalieRadius { get; } = 0.7f;

	public float GoalieSaveCreaseSystemZDelta { get; } = 0.0125f;

	public bool SaveEOGJSON { get; } = true;

	public int MaxTippedMilliseconds { get; } = 91;

	public int MinPossessionMilliseconds { get; } = 300;

	public int MaxPossessionMilliseconds { get; } = 700;

	public int TurnoverThresholdMilliseconds { get; } = 500;

	public void UpdateDefaultValues(ISubConfig oldConfig)
	{
		throw new NotImplementedException();
	}
}
