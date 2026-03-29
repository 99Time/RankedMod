using System.Collections.Generic;
using UnityEngine;

public class UIModsController : MonoBehaviour
{
	private UIMods uiMods;

	private void Awake()
	{
		uiMods = GetComponent<UIMods>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnModAdded", Event_Client_OnModAdded);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnModChanged", Event_Client_OnModChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnModRemoved", Event_Client_OnModRemoved);
		uiMods.ClearMods();
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnModAdded", Event_Client_OnModAdded);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnModChanged", Event_Client_OnModChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnModRemoved", Event_Client_OnModRemoved);
	}

	private void Event_Client_OnModAdded(Dictionary<string, object> message)
	{
		Mod mod = (Mod)message["mod"];
		uiMods.AddMod(mod);
	}

	private void Event_Client_OnModChanged(Dictionary<string, object> message)
	{
		Mod mod = (Mod)message["mod"];
		Debug.Log($"[UIModsController] Mod changed, updating mod {mod.InstalledItem.Id}");
		uiMods.UpdateMod(mod);
	}

	private void Event_Client_OnModRemoved(Dictionary<string, object> message)
	{
		Mod mod = (Mod)message["mod"];
		uiMods.RemoveMod(mod);
	}
}
