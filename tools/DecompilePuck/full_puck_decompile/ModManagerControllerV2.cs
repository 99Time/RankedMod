using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ModManagerControllerV2 : MonoBehaviour
{
	private ModManagerV2 modManager;

	public void Awake()
	{
		modManager = GetComponent<ModManagerV2>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnInstalledItemAdded", Event_Client_OnInstalledItemAdded);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnInstalledItemRemoved", Event_Client_OnInstalledItemRemoved);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnModAdded", Event_Client_OnModAdded);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnModClickEnable", Event_Client_OnModClickEnable);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnModClickDisable", Event_Client_OnModClickDisable);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnConnectionRejected", Event_Client_OnConnectionRejected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnDisconnected", Event_Client_OnDisconnected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnModEnableSucceeded", Event_Client_OnModEnableSucceeded);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnModEnableFailed", Event_Client_OnModEnableFailed);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnModDisableSucceeded", Event_Client_OnModDisableSucceeded);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnItemSubscribeFailed", Event_Client_OnItemSubscribeFailed);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnItemDownloadFailed", Event_Client_OnItemDownloadFailed);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPendingModsSet", Event_Client_OnPendingModsSet);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPopupClickClose", Event_Client_OnPopupClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnItemIntegrityVerified", Event_Client_OnItemIntegrityVerified);
		modManager.LoadModsState();
		StartCoroutine(LateStart());
	}

	private IEnumerator LateStart()
	{
		yield return new WaitForEndOfFrame();
		modManager.LoadPlugins();
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnInstalledItemAdded", Event_Client_OnInstalledItemAdded);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnInstalledItemRemoved", Event_Client_OnInstalledItemRemoved);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnModAdded", Event_Client_OnModAdded);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnModClickEnable", Event_Client_OnModClickEnable);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnModClickDisable", Event_Client_OnModClickDisable);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnConnectionRejected", Event_Client_OnConnectionRejected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnDisconnected", Event_Client_OnDisconnected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnModEnableSucceeded", Event_Client_OnModEnableSucceeded);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnModEnableFailed", Event_Client_OnModEnableFailed);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnModDisableSucceeded", Event_Client_OnModDisableSucceeded);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnItemSubscribeFailed", Event_Client_OnItemSubscribeFailed);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnItemDownloadFailed", Event_Client_OnItemDownloadFailed);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPendingModsSet", Event_Client_OnPendingModsSet);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPopupClickClose", Event_Client_OnPopupClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnItemIntegrityVerified", Event_Client_OnItemIntegrityVerified);
	}

	private void Event_Client_OnInstalledItemAdded(Dictionary<string, object> message)
	{
		InstalledItem installedItem = (InstalledItem)message["installedItem"];
		modManager.AddMod(installedItem);
	}

	private void Event_Client_OnInstalledItemRemoved(Dictionary<string, object> message)
	{
		InstalledItem installedItem = (InstalledItem)message["installedItem"];
		modManager.RemoveMod(installedItem);
	}

	private void Event_Client_OnModAdded(Dictionary<string, object> message)
	{
		Mod mod = (Mod)message["mod"];
		if (Application.isBatchMode)
		{
			mod.Enable();
			return;
		}
		if (mod.IsPlugin)
		{
			mod.Enable();
		}
		if (modManager.PendingModIds.Contains(mod.InstalledItem.Id))
		{
			mod.Enable();
		}
		if (modManager.GetModState(mod.InstalledItem.Id))
		{
			mod.Enable();
		}
	}

	private void Event_Client_OnModClickEnable(Dictionary<string, object> message)
	{
		((Mod)message["mod"]).Enable(isManual: true);
	}

	private void Event_Client_OnModClickDisable(Dictionary<string, object> message)
	{
		((Mod)message["mod"]).Disable(isManual: true);
	}

	private void Event_Client_OnConnectionRejected(Dictionary<string, object> message)
	{
		ConnectionRejection connectionRejection = (ConnectionRejection)message["connectionRejection"];
		if (connectionRejection.code == ConnectionRejectionCode.MissingMods)
		{
			modManager.SetPendingMods(connectionRejection.clientRequiredModIds);
		}
		else
		{
			modManager.SetModsToState();
		}
	}

	private void Event_Client_OnDisconnected(Dictionary<string, object> message)
	{
		modManager.SetModsToState();
	}

	private void Event_Client_OnModEnableSucceeded(Dictionary<string, object> message)
	{
		Mod mod = (Mod)message["mod"];
		bool flag = (bool)message["isManual"];
		if (!mod.IsPlugin)
		{
			if (flag)
			{
				modManager.SetModState(mod.InstalledItem.Id, state: true);
			}
			if (modManager.PendingModIds.Contains(mod.InstalledItem.Id))
			{
				modManager.RemovePendingMod(mod.InstalledItem.Id);
			}
		}
	}

	private void Event_Client_OnModEnableFailed(Dictionary<string, object> message)
	{
		Mod mod = (Mod)message["mod"];
		if (modManager.PendingModIds.Contains(mod.InstalledItem.Id))
		{
			modManager.ResetPendingMods($"Installation failed for {mod.InstalledItem.Id}");
		}
	}

	private void Event_Client_OnModDisableSucceeded(Dictionary<string, object> message)
	{
		Mod mod = (Mod)message["mod"];
		bool flag = (bool)message["isManual"];
		if (!mod.IsPlugin && flag)
		{
			modManager.SetModState(mod.InstalledItem.Id, state: false);
		}
	}

	private void Event_Client_OnItemSubscribeFailed(Dictionary<string, object> message)
	{
		ulong num = (ulong)message["itemId"];
		if (modManager.PendingModIds.Contains(num))
		{
			modManager.ResetPendingMods($"Subscription failed for {num}");
		}
	}

	private void Event_Client_OnItemDownloadFailed(Dictionary<string, object> message)
	{
		ulong num = (ulong)message["itemId"];
		if (modManager.PendingModIds.Contains(num))
		{
			modManager.ResetPendingMods($"Download failed for {num}");
		}
	}

	private void Event_Client_OnPendingModsSet(Dictionary<string, object> message)
	{
		PendingMod[] array = (PendingMod[])message["pendingMods"];
		foreach (PendingMod pendingMod in array)
		{
			if (pendingMod.Mod != null && !pendingMod.Mod.IsEnabled)
			{
				pendingMod.Mod.Enable();
			}
		}
	}

	private void Event_Client_OnPopupClickClose(Dictionary<string, object> message)
	{
		if (!((string)message["name"] != "pendingMods"))
		{
			modManager.ResetPendingMods("Installation cancelled");
		}
	}

	private void Event_Client_OnItemIntegrityVerified(Dictionary<string, object> message)
	{
		List<InstalledItem> source = (List<InstalledItem>)message["installedItems"];
		modManager.VerifyModsState(source.Select((InstalledItem item) => item.Id).ToArray());
	}
}
