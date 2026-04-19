namespace Codebase;

public static class TeamFunc
{
	public const PlayerTeam DEFAULT_TEAM = (PlayerTeam)2;

	public static PlayerTeam GetOtherTeam(PlayerTeam team)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Invalid comparison between Unknown and I4
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		if ((int)team != 2)
		{
			if ((int)team != 3)
			{
				return (PlayerTeam)0;
			}
			return (PlayerTeam)2;
		}
		return (PlayerTeam)3;
	}
}
