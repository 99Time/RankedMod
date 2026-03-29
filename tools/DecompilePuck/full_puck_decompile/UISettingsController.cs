using System.Collections.Generic;
using UnityEngine;

public class UISettingsController : MonoBehaviour
{
	private UISettings uiSettings;

	private void Awake()
	{
		uiSettings = GetComponent<UISettings>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnDisplayIndexChanged", Event_Client_OnDisplayIndexChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnKeyBindsLoaded", Event_Client_OnKeyBindsLoaded);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsResetToDefault", Event_Client_OnSettingsResetToDefault);
		uiSettings.ApplySettingsValues();
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnDisplayIndexChanged", Event_Client_OnDisplayIndexChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnKeyBindsLoaded", Event_Client_OnKeyBindsLoaded);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsResetToDefault", Event_Client_OnSettingsResetToDefault);
	}

	private void Event_Client_OnDisplayIndexChanged(Dictionary<string, object> message)
	{
		int index = (int)message["resolutionIndex"];
		string displayStringFromIndex = Utils.GetDisplayStringFromIndex((int)message["displayIndex"]);
		string resolutionStringFromIndex = Utils.GetResolutionStringFromIndex(index);
		uiSettings.UpdateDisplayDropdown(displayStringFromIndex);
		uiSettings.UpdateResolutionsDropdown(resolutionStringFromIndex);
	}

	private void Event_Client_OnKeyBindsLoaded(Dictionary<string, object> message)
	{
		Dictionary<string, KeyBind> keyBinds = (Dictionary<string, KeyBind>)message["keyBinds"];
		uiSettings.UpdateKeyBinds(keyBinds);
	}

	private void Event_Client_OnSettingsResetToDefault(Dictionary<string, object> message)
	{
		uiSettings.ApplySettingsValues();
	}
}
