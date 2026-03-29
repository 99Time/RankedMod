using System;
using System.Net;
using Steamworks;
using UnityEngine;

public class SteamManager : MonoBehaviourSingleton<SteamManager>
{
	[HideInInspector]
	public bool IsInitialized;

	private static Callback<SteamServersConnected_t> steamServersConnectedCallback;

	private static Callback<SteamServerConnectFailure_t> steamServerConnectFailureCallback;

	private static Callback<SteamServersDisconnected_t> steamServersDisconnectedCallback;

	public override void Awake()
	{
		base.Awake();
		if (Application.isBatchMode)
		{
			IsInitialized = GameServer.Init(BitConverter.ToUInt32(IPAddress.Any.GetAddressBytes(), 0), 0, 0, EServerMode.eServerModeNoAuthentication, null);
			if (IsInitialized)
			{
				SteamGameServer.LogOnAnonymous();
			}
		}
		else
		{
			IsInitialized = SteamAPI.Init();
		}
		if (IsInitialized)
		{
			if (Application.isBatchMode)
			{
				Debug.Log("[SteamManager] Initialized as GameServer");
			}
			else
			{
				Debug.Log("[SteamManager] Initialized as SteamClient");
			}
			RegisterCallbacks();
		}
		else if (Application.isBatchMode)
		{
			Debug.LogError("[SteamManager] Failed to initialize GameServer");
		}
		else
		{
			Debug.LogError("[SteamManager] Failed to initialize SteamClient");
		}
	}

	private void RegisterCallbacks()
	{
		if (IsInitialized)
		{
			if (Application.isBatchMode)
			{
				steamServersConnectedCallback = Callback<SteamServersConnected_t>.CreateGameServer(OnSteamServersConnected);
				steamServerConnectFailureCallback = Callback<SteamServerConnectFailure_t>.CreateGameServer(OnSteamServerConnectFailure);
				steamServersDisconnectedCallback = Callback<SteamServersDisconnected_t>.CreateGameServer(OnSteamServersDisconnected);
			}
			Debug.Log("[SteamManager] Registered callbacks");
		}
	}

	private void OnDestroy()
	{
		if (IsInitialized)
		{
			if (Application.isBatchMode)
			{
				GameServer.Shutdown();
			}
			else
			{
				SteamAPI.Shutdown();
			}
		}
	}

	private void OnApplicationQuit()
	{
		if (IsInitialized)
		{
			if (Application.isBatchMode)
			{
				GameServer.Shutdown();
			}
			else
			{
				SteamAPI.Shutdown();
			}
		}
	}

	private void Update()
	{
		if (IsInitialized)
		{
			if (Application.isBatchMode)
			{
				GameServer.RunCallbacks();
			}
			else
			{
				SteamAPI.RunCallbacks();
			}
		}
	}

	private static void OnSteamServersConnected(SteamServersConnected_t callback)
	{
		Debug.Log("[SteamManager] Connected to Steam");
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSteamServersConnected");
	}

	private static void OnSteamServerConnectFailure(SteamServerConnectFailure_t callback)
	{
		Debug.Log($"[SteamManager] Failed to connect to Steam: {callback.m_eResult}");
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSteamServerConnectFailure");
	}

	private static void OnSteamServersDisconnected(SteamServersDisconnected_t callback)
	{
		Debug.Log($"[SteamManager] Disconnected from Steam: {callback.m_eResult}");
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSteamServersDisconnected");
	}
}
