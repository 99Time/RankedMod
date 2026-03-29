using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public class SteamIntegrationManager : MonoBehaviourSingleton<SteamIntegrationManager>
{
	private Callback<GetTicketForWebApiResponse_t> GetTicketForWebApiCallback;

	private Callback<MicroTxnAuthorizationResponse_t> MicroTxnAuthorizationResponse;

	private Callback<GameRichPresenceJoinRequested_t> GameRichPresenceJoinRequested;

	private Callback<NewUrlLaunchParameters_t> NewUrlLaunchParameters;

	private void Start()
	{
		RegisterCallbacks();
	}

	private void RegisterCallbacks()
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			GetTicketForWebApiCallback = Callback<GetTicketForWebApiResponse_t>.Create(OnGotAuthTicketForWebApi);
			MicroTxnAuthorizationResponse = Callback<MicroTxnAuthorizationResponse_t>.Create(OnMicroTxnAuthorizationResponse);
			GameRichPresenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnGameRichPresenceJoinRequested);
			NewUrlLaunchParameters = Callback<NewUrlLaunchParameters_t>.Create(OnNewUrlLaunchParameters);
		}
	}

	public void SubscribeItem(ulong itemId)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			SteamUGC.SubscribeItem(new PublishedFileId_t
			{
				m_PublishedFileId = itemId
			});
		}
	}

	public void DownloadItem(ulong itemId)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			SteamUGC.DownloadItem(new PublishedFileId_t
			{
				m_PublishedFileId = itemId
			}, bHighPriority: true);
		}
	}

	public void SetRichPresenceMainMenu()
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			SteamFriends.ClearRichPresence();
			SteamFriends.SetRichPresence("steam_display", "#Status_MainMenu");
			SteamFriends.SetRichPresence("status", "In the changing room");
		}
	}

	public void SetRichPresenceSpectating(Server server, int playerCount)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			SteamFriends.SetRichPresence("steam_player_group", $"{server.IpAddress}:{server.Port}");
			SteamFriends.SetRichPresence("steam_player_group_size", $"{playerCount}");
			SteamFriends.SetRichPresence("steam_display", "#Status_Spectating");
			SteamFriends.SetRichPresence("status", "Spectating");
			SteamFriends.SetRichPresence("connect", $"+ipAddress {server.IpAddress} +port {server.Port}");
		}
	}

	public void SetRichPresencePlaying(Server server, int playerCount)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			SteamFriends.SetRichPresence("steam_player_group", $"{server.IpAddress}:{server.Port}");
			SteamFriends.SetRichPresence("steam_player_group_size", $"{playerCount}");
			SteamFriends.SetRichPresence("steam_display", "#Status_Playing");
			SteamFriends.SetRichPresence("status", "Playing");
			SteamFriends.SetRichPresence("connect", $"+ipAddress {server.IpAddress} +port {server.Port}");
		}
	}

	public void UpdateRichPresenceScore(bool show, int period, int blueScore, int redScore)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			string pchValue = (show ? $" | P{period} {blueScore} - {redScore}" : " ");
			SteamFriends.SetRichPresence("score", pchValue);
		}
	}

	public void UpdateRichPresenceRole(PlayerRole role)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			string pchValue = role.ToString().Replace("Attacker", "Skater");
			SteamFriends.SetRichPresence("role", pchValue);
		}
	}

	public void UpdateRichPresenceTeam(PlayerTeam team)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			string pchValue = team.ToString().Replace("Blue", "Team Blue").Replace("Red", "Team Red");
			SteamFriends.SetRichPresence("team", pchValue);
		}
	}

	public void UpdateRichPresencePhase(GamePhase phase)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			string pchValue = ((phase != GamePhase.Warmup) ? "Playing" : "Warming up");
			SteamFriends.SetRichPresence("phase", pchValue);
		}
	}

	public void GetAuthTicketForWebApi()
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			Debug.Log("[SteamIntegrationManager] Getting Steam Auth Ticket for Web API");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnGetAuthTicketForWebApi");
			SteamUser.GetAuthTicketForWebApi(null);
		}
	}

	public void GetLaunchCommandLine()
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			SteamApps.GetLaunchCommandLine(out var pszCommandLine, 256);
			string[] array = pszCommandLine.Split(" ");
			if (array.Length != 0)
			{
				Debug.Log($"[SteamIntegrationManager] GotLaunchCommandLine: {pszCommandLine} ({array.Length})");
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnGotLaunchCommandLine", new Dictionary<string, object> { { "args", array } });
			}
		}
	}

	private void OnGotAuthTicketForWebApi(GetTicketForWebApiResponse_t response)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			byte[] rgubTicket = response.m_rgubTicket;
			string value = BitConverter.ToString(rgubTicket, 0, rgubTicket.Length).Replace("-", string.Empty);
			Debug.Log("[SteamIntegrationManager] GotAuthTicketForWebApi");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnGotAuthTicketForWebApi", new Dictionary<string, object> { { "ticket", value } });
		}
	}

	private void OnMicroTxnAuthorizationResponse(MicroTxnAuthorizationResponse_t response)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			Debug.Log($"[SteamIntegrationManager] MicroTxnAuthorizationResponse: {response.m_unAppID} {response.m_ulOrderID} {response.m_bAuthorized}");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMicroTxnAuthorizationResponse", new Dictionary<string, object>
			{
				{ "orderId", response.m_ulOrderID },
				{
					"authorized",
					Convert.ToBoolean(response.m_bAuthorized)
				}
			});
		}
	}

	private void OnGameRichPresenceJoinRequested(GameRichPresenceJoinRequested_t response)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			Debug.Log("[SteamIntegrationManager] GameRichPresenceJoinRequested: " + response.m_rgchConnect);
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnGameRichPresenceJoinRequested", new Dictionary<string, object> { 
			{
				"args",
				response.m_rgchConnect.Split(" ")
			} });
		}
	}

	private void OnNewUrlLaunchParameters(NewUrlLaunchParameters_t response)
	{
		if (MonoBehaviourSingleton<SteamManager>.Instance.IsInitialized)
		{
			GetLaunchCommandLine();
		}
	}
}
