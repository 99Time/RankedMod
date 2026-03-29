using System.Collections.Generic;
using UnityEngine;

public class UIMainMenuController : MonoBehaviour
{
	private UIMainMenu uiMainMenu;

	private void Awake()
	{
		uiMainMenu = GetComponent<UIMainMenu>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnDebugChanged", Event_Client_OnDebugChanged);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnDebugChanged", Event_Client_OnDebugChanged);
	}

	private void Event_Client_OnDebugChanged(Dictionary<string, object> message)
	{
		if ((int)message["value"] > 0)
		{
			uiMainMenu.ShowDebugTools();
		}
		else
		{
			uiMainMenu.HideDebugTools();
		}
	}
}
