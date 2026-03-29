using System;
using System.Collections.Generic;
using System.Linq;

public class StateManager : MonoBehaviourSingleton<StateManager>
{
	private PlayerData playerData;

	public PlayerData PlayerData
	{
		get
		{
			if (playerData != null)
			{
				return playerData;
			}
			return new PlayerData();
		}
		set
		{
			if (playerData != value)
			{
				PlayerData oldPlayerData = playerData;
				playerData = value;
				OnPlayerDataChanged(oldPlayerData);
			}
		}
	}

	public double CurrentUnixTimestamp => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;

	public bool IsBanned => Ban(playerData) != null;

	public bool IsMuted => Mute(playerData) != null;

	public PlayerBan Ban(PlayerData playerData)
	{
		return playerData.bans.FirstOrDefault((PlayerBan ban) => CurrentUnixTimestamp <= ban.until);
	}

	public PlayerMute Mute(PlayerData playerData)
	{
		return playerData.mutes.FirstOrDefault((PlayerMute mute) => CurrentUnixTimestamp <= mute.until);
	}

	public PlayerBan IsPlayerDataBanned(PlayerData playerData)
	{
		return playerData.bans.FirstOrDefault((PlayerBan ban) => CurrentUnixTimestamp <= ban.until);
	}

	public PlayerMute IsPlayerDataMuted(PlayerData playerData)
	{
		return playerData.mutes.FirstOrDefault((PlayerMute mute) => CurrentUnixTimestamp <= mute.until);
	}

	private void OnPlayerDataChanged(PlayerData oldPlayerData)
	{
		if (playerData != null)
		{
			if (oldPlayerData == null)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerDataReady", new Dictionary<string, object>
				{
					{ "oldPlayerData", oldPlayerData },
					{ "newPlayerData", PlayerData }
				});
			}
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerDataChanged", new Dictionary<string, object>
			{
				{ "oldPlayerData", oldPlayerData },
				{ "newPlayerData", PlayerData }
			});
			PlayerBan playerBan = Ban(playerData);
			PlayerMute playerMute = Mute(playerData);
			bool flag = oldPlayerData != null && Ban(oldPlayerData) != null;
			bool flag2 = oldPlayerData != null && Mute(oldPlayerData) != null;
			if (IsBanned && !flag)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerBanned", new Dictionary<string, object>
				{
					{ "reason", playerBan.reason },
					{ "until", playerBan.until }
				});
			}
			else if (!IsBanned && flag)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerUnbanned");
			}
			if (IsMuted && !flag2)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerMuted", new Dictionary<string, object>
				{
					{ "reason", playerMute.reason },
					{ "until", playerMute.until }
				});
			}
			else if (!IsMuted && flag2)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerUnmuted");
			}
		}
	}
}
