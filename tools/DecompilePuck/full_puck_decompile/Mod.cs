using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;

public class Mod : INotifyPropertyChanged
{
	public InstalledItem InstalledItem;

	private bool isEnabled;

	private Texture2D previewTexture;

	private bool isAssemblyMod;

	private bool isPlugin;

	private ModManagerV2 modManager;

	private string assemblyPath;

	private Assembly assembly;

	private object instance;

	private MethodInfo onEnableMethod;

	private MethodInfo onDisableMethod;

	private bool isDownloadingPreviewTexture;

	public bool IsEnabled
	{
		get
		{
			return isEnabled;
		}
		set
		{
			if (isEnabled != value)
			{
				isEnabled = value;
				NotifyPropertyChanged("IsEnabled");
			}
		}
	}

	public Texture2D PreviewTexture
	{
		get
		{
			return previewTexture;
		}
		set
		{
			if (!(previewTexture == value))
			{
				previewTexture = value;
				NotifyPropertyChanged("PreviewTexture");
			}
		}
	}

	public bool IsAssemblyMod
	{
		get
		{
			return isAssemblyMod;
		}
		set
		{
			if (isAssemblyMod != value)
			{
				isAssemblyMod = value;
				NotifyPropertyChanged("IsAssemblyMod");
			}
		}
	}

	public bool IsPlugin
	{
		get
		{
			return isPlugin;
		}
		set
		{
			if (isPlugin != value)
			{
				isPlugin = value;
				NotifyPropertyChanged("IsPlugin");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public Mod(ModManagerV2 modManager, InstalledItem installedItem)
	{
		this.modManager = modManager;
		InstalledItem = installedItem;
		InstalledItem.PropertyChanged += OnInstalledItemPropertyChanged;
		PropertyChanged += OnModPropertyChanged;
		assemblyPath = (from path in Directory.GetFiles(InstalledItem.Path, "*.dll", SearchOption.TopDirectoryOnly)
			orderby path
			select path).FirstOrDefault();
		IsAssemblyMod = assemblyPath != null;
	}

	private void LoadAssembly()
	{
		if (instance == null)
		{
			assembly = Assembly.LoadFile(assemblyPath);
			Type type = assembly.GetTypes().FirstOrDefault((Type type2) => type2.IsClass && !type2.IsAbstract && typeof(IPuckMod).IsAssignableFrom(type2));
			if (type == null)
			{
				throw new Exception("IPuckMod missing from assembly");
			}
			instance = Activator.CreateInstance(type);
			onEnableMethod = type.GetMethod("OnEnable");
			onDisableMethod = type.GetMethod("OnDisable");
			Debug.Log($"[Mod] Loaded assembly for mod {InstalledItem.Id}");
		}
	}

	public void Enable(bool isManual = false)
	{
		if (IsEnabled)
		{
			return;
		}
		try
		{
			if (IsAssemblyMod)
			{
				LoadAssembly();
				if (!(bool)onEnableMethod.Invoke(instance, null))
				{
					throw new Exception("OnEnable returned false");
				}
			}
			IsEnabled = true;
			Debug.Log($"[Mod] Enabled mod {InstalledItem.Id}");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModEnableSucceeded", new Dictionary<string, object>
			{
				{ "mod", this },
				{ "isManual", isManual }
			});
		}
		catch (Exception ex)
		{
			Debug.LogError($"[Mod] Failed to enable mod {InstalledItem.Id}: {ex.Message}");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModEnableFailed", new Dictionary<string, object>
			{
				{ "mod", this },
				{ "isManual", isManual }
			});
		}
	}

	public void Disable(bool isManual = false)
	{
		if (!IsEnabled)
		{
			return;
		}
		Debug.Log($"[Mod] Disabling mod {InstalledItem.Id}...");
		try
		{
			if (IsAssemblyMod && !(bool)onDisableMethod.Invoke(instance, null))
			{
				throw new Exception("OnDisable returned false");
			}
			IsEnabled = false;
			Debug.Log($"[Mod] Disabled mod {InstalledItem.Id}");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModDisableSucceeded", new Dictionary<string, object>
			{
				{ "mod", this },
				{ "isManual", isManual }
			});
		}
		catch (Exception ex)
		{
			Debug.LogError($"[Mod] Failed to disable mod {InstalledItem.Id}: {ex.Message}");
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModDisableFailed", new Dictionary<string, object>
			{
				{ "mod", this },
				{ "isManual", isManual }
			});
		}
	}

	public void Dispose()
	{
		Disable();
		InstalledItem.PropertyChanged -= OnInstalledItemPropertyChanged;
		PropertyChanged -= OnModPropertyChanged;
	}

	private IEnumerator DownloadPreviewTexture()
	{
		UnityWebRequest www = UnityWebRequestTexture.GetTexture(InstalledItem.ItemDetails.PreviewUrl);
		yield return www.SendWebRequest();
		if (www.result != UnityWebRequest.Result.Success)
		{
			Debug.LogError("[Mod] Failed to download preview texture: " + www.error);
		}
		else
		{
			PreviewTexture = DownloadHandlerTexture.GetContent(www);
		}
		isDownloadingPreviewTexture = false;
	}

	public void OnInstalledItemPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModChanged", new Dictionary<string, object> { { "mod", this } });
		if (!isDownloadingPreviewTexture && InstalledItem.ItemDetails != null && InstalledItem.ItemDetails.PreviewUrl != null && PreviewTexture == null)
		{
			isDownloadingPreviewTexture = true;
			modManager.StartCoroutine(DownloadPreviewTexture());
		}
	}

	public void OnModPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnModChanged", new Dictionary<string, object> { { "mod", this } });
	}

	private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
