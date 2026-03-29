using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SteamWorkshopManagerController : MonoBehaviour
{
	private SteamWorkshopManager steamWorkshopManager;

	private void Awake()
	{
		steamWorkshopManager = GetComponent<SteamWorkshopManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSteamServersConnected", Event_Client_OnSteamServersConnected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnItemDownloadSucceeded", Event_Client_OnItemDownloadSucceeded);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPendingModsSet", Event_Client_OnPendingModsSet);
		StartCoroutine(LateStart());
	}

	private IEnumerator LateStart()
	{
		yield return new WaitForEndOfFrame();
		steamWorkshopManager.VerifyItemIntegrity();
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSteamServersConnected", Event_Client_OnSteamServersConnected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnItemDownloadSucceeded", Event_Client_OnItemDownloadSucceeded);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPendingModsSet", Event_Client_OnPendingModsSet);
	}

	private void Event_Client_OnSteamServersConnected(Dictionary<string, object> message)
	{
		ulong[] enabledModIds = NetworkBehaviourSingleton<ServerManager>.Instance.ServerConfigurationManager.EnabledModIds;
		foreach (ulong itemId in enabledModIds)
		{
			steamWorkshopManager.DownloadItem(itemId);
		}
	}

	private void Event_Client_OnItemDownloadSucceeded(Dictionary<string, object> message)
	{
		steamWorkshopManager.VerifyItemIntegrity();
	}

	private void Event_Client_OnPendingModsSet(Dictionary<string, object> message)
	{
		PendingMod[] array = (PendingMod[])message["pendingMods"];
		foreach (PendingMod pendingMod in array)
		{
			if (pendingMod.Mod == null)
			{
				steamWorkshopManager.SubscribeItem(pendingMod.Id);
			}
		}
	}
}
