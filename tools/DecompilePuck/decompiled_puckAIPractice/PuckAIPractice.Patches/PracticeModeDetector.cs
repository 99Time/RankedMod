using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using PuckAIPractice.Singletons;
using Unity.Netcode;
using UnityEngine;

namespace PuckAIPractice.Patches;

[HarmonyPatch(typeof(WebSocketManager))]
public static class PracticeModeDetector
{
	[HarmonyPatch(typeof(ConnectionManager))]
	public static class ConnectionManagerPracticePatches
	{
		[HarmonyPostfix]
		[HarmonyPatch("Client_StartClient")]
		private static void AfterStartClient(ConnectionManager __instance, string ipAddress, ushort port, string password)
		{
			string connectionDataJson = null;
			if ((Object)(object)__instance != (Object)null && (Object)(object)NetworkManager.Singleton != (Object)null)
			{
				connectionDataJson = Encoding.ASCII.GetString(NetworkManager.Singleton.NetworkConfig.ConnectionData);
			}
			OnClientStart(ipAddress, port, password, connectionDataJson);
		}

		[HarmonyPostfix]
		[HarmonyPatch("Client_Disconnect")]
		private static void AfterDisconnect()
		{
			OnClientDisconnect();
		}
	}

	public static bool IsPracticeMode { get; set; }

	[HarmonyPostfix]
	[HarmonyPatch("Emit")]
	private static void Emit_Postfix(string messageName, Dictionary<string, object> data, string responseMessageName)
	{
		try
		{
			if (messageName == "serverAuthenticateRequest" && data != null && data.ContainsKey("name"))
			{
				string text = data["name"]?.ToString() ?? "";
				IsPracticeMode = text.ToUpperInvariant() == "PRACTICE";
				if (ConfigData.Instance.StartWithBlueGoalie)
				{
				}
				if (!ConfigData.Instance.StartWithRedGoalie)
				{
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public static void OnClientStart(string ip, ushort port, string password, string connectionDataJson)
	{
		IsPracticeMode = false;
		try
		{
			if (connectionDataJson != null && !connectionDataJson.Contains("\"name\":\"PRACTICE\""))
			{
			}
		}
		catch
		{
		}
	}

	public static void OnClientDisconnect()
	{
		if (IsPracticeMode)
		{
			IsPracticeMode = false;
		}
	}
}
