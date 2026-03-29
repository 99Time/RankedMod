using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;

public class ModManagerV2 : MonoBehaviourSingleton<ModManagerV2>
{
	[HideInInspector]
	public List<Mod> Mods = new List<Mod>();

	public Dictionary<ulong, bool> ModsState = new Dictionary<ulong, bool>();

	public List<PendingMod> pendingMods = new List<PendingMod>();

	[HideInInspector]
	public ulong[] InstalledModIds => Mods.Select((Mod mod) => mod.InstalledItem.Id).ToArray();

	[HideInInspector]
	public ulong[] EnabledModIds => (from mod in Mods
		where mod.IsEnabled
		select mod.InstalledItem.Id).ToArray();

	[HideInInspector]
	public ulong[] DisabledModIds => (from mod in Mods
		where !mod.IsEnabled
		select mod.InstalledItem.Id).ToArray();

	[HideInInspector]
	public ulong[] PendingModIds => pendingMods.Select((PendingMod pm) => pm.Id).ToArray();

	private string pluginsPath => Path.Combine(Path.GetFullPath("."), "Plugins");

	public void LoadPlugins()
	{
		Debug.Log("[ModManagerV2] Loading plugins from " + pluginsPath);
		if (!Directory.Exists(pluginsPath))
		{
			Directory.CreateDirectory(pluginsPath);
		}
		string[] directories = Directory.GetDirectories(pluginsPath);
		foreach (string path in directories)
		{
			InstalledItem installedItem = new InstalledItem((ulong)Mods.Count, path);
			AddMod(installedItem, isPlugin: true);
			string fileName = Path.GetFileName(path);
			installedItem.ItemDetails = new ItemDetails
			{
				Title = fileName
			};
		}
	}

	public void AddMod(InstalledItem installedItem, bool isPlugin = false)
	{
		Debug.Log($"[ModManagerV2] Adding mod {installedItem.Id} from {installedItem.Path}");
		Mod mod = new Mod(this, installedItem);
		mod.IsPlugin = isPlugin;
		Mods.Add(mod);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModAdded", new Dictionary<string, object> { { "mod", mod } });
	}

	public void RemoveMod(InstalledItem installedItem)
	{
		Debug.Log($"[ModManagerV2] Removing mod {installedItem.Id}");
		Mod modByInstalledItem = GetModByInstalledItem(installedItem);
		if (modByInstalledItem != null)
		{
			modByInstalledItem.Dispose();
			Mods.Remove(modByInstalledItem);
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModRemoved", new Dictionary<string, object> { { "mod", modByInstalledItem } });
		}
	}

	public Mod GetModByInstalledItem(InstalledItem installedItem)
	{
		return Mods.Find((Mod m) => m.InstalledItem == installedItem);
	}

	public Mod GetModById(ulong id)
	{
		return Mods.Find((Mod m) => m.InstalledItem.Id == id);
	}

	public void SetPendingMods(ulong[] ids)
	{
		Debug.Log("[ModManagerV2] Setting pending mods: " + string.Join(", ", ids));
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnBeforePendingModsSet", new Dictionary<string, object> { { "ids", ids } });
		foreach (ulong id in ids)
		{
			Mod modById = GetModById(id);
			if (modById == null || !modById.IsEnabled)
			{
				pendingMods.Add(new PendingMod(id, modById));
			}
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPendingModsSet", new Dictionary<string, object> { 
		{
			"pendingMods",
			pendingMods.ToArray()
		} });
	}

	public void ResetPendingMods(string reason = null)
	{
		pendingMods.Clear();
		Debug.Log("[ModManagerV2] Pending mods reset: " + reason);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPendingModsReset", new Dictionary<string, object> { { "reason", reason } });
	}

	public void RemovePendingMod(ulong id)
	{
		PendingMod pendingMod = pendingMods.Find((PendingMod pm) => pm.Id == id);
		if (pendingMod != null)
		{
			pendingMods.Remove(pendingMod);
			Debug.Log($"[ModManagerV2] Pending mod {id} removed");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPendingModRemoved", new Dictionary<string, object> { { "pendingMod", pendingMod } });
			if (pendingMods.Count == 0)
			{
				Debug.Log("[ModManagerV2] Pending mods cleared");
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPendingModsCleared");
			}
		}
	}

	public void LoadModsState()
	{
		string text = PlayerPrefs.GetString("modsState", null);
		if (string.IsNullOrEmpty(text))
		{
			SaveModsState();
			LoadModsState();
		}
		else
		{
			ModsState = JsonSerializer.Deserialize<Dictionary<ulong, bool>>(text);
		}
	}

	public void VerifyModsState(ulong[] installedItemIds)
	{
		ModsState.Where((KeyValuePair<ulong, bool> modState) => !installedItemIds.Contains(modState.Key)).ToList().ForEach(delegate(KeyValuePair<ulong, bool> modState)
		{
			ModsState.Remove(modState.Key);
		});
		SaveModsState();
	}

	public void SetModsToState()
	{
		foreach (Mod mod in Mods)
		{
			bool modState = GetModState(mod.InstalledItem.Id);
			if (!mod.IsPlugin)
			{
				if (modState)
				{
					mod.Enable();
				}
				else
				{
					mod.Disable();
				}
			}
		}
	}

	public void SaveModsState()
	{
		string value = JsonSerializer.Serialize(ModsState, new JsonSerializerOptions
		{
			WriteIndented = true
		});
		PlayerPrefs.SetString("modsState", value);
	}

	public void SetModState(ulong id, bool state)
	{
		if (ModsState.ContainsKey(id))
		{
			ModsState[id] = state;
		}
		else
		{
			ModsState.Add(id, state);
		}
		SaveModsState();
	}

	public bool GetModState(ulong id)
	{
		if (!ModsState.ContainsKey(id))
		{
			ModsState.Add(id, value: false);
			SaveModsState();
		}
		return ModsState[id];
	}

	private void OnApplicationQuit()
	{
		foreach (Mod mod in Mods)
		{
			mod.Dispose();
		}
		Mods.Clear();
	}
}
