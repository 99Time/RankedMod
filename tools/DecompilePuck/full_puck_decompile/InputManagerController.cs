using System.Collections.Generic;
using UnityEngine;

public class InputManagerController : MonoBehaviour
{
	private InputManager inputManager;

	private void Awake()
	{
		inputManager = GetComponent<InputManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsKeyBindClicked", Event_Client_OnSettingsKeyBindClicked);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsKeyBindTypeChanged", Event_Client_OnSettingsKeyBindTypeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsResetToDefault", Event_Client_OnSettingsResetToDefault);
		inputManager.LoadKeyBinds();
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsKeyBindClicked", Event_Client_OnSettingsKeyBindClicked);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsKeyBindTypeChanged", Event_Client_OnSettingsKeyBindTypeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsResetToDefault", Event_Client_OnSettingsResetToDefault);
	}

	private void Event_Client_OnSettingsKeyBindClicked(Dictionary<string, object> message)
	{
		string actionName = (string)message["actionName"];
		inputManager.RebindButtonInteractively(actionName);
	}

	private void Event_Client_OnSettingsKeyBindTypeChanged(Dictionary<string, object> message)
	{
		string text = (string)message["actionName"];
		string humanizedInteraction = (string)message["type"];
		if (inputManager.RebindableInputActions.ContainsKey(text) && !(inputManager.RebindableInputActions[text].bindings[0].effectiveInteractions == Utils.GetInteractionFromHumanizedInteraction(humanizedInteraction)))
		{
			inputManager.SetActionInteractions(text, Utils.GetInteractionFromHumanizedInteraction(humanizedInteraction));
			inputManager.SaveKeyBinds();
		}
	}

	private void OnApplicationQuit()
	{
		inputManager.SaveKeyBinds();
	}

	private void Event_Client_OnSettingsResetToDefault(Dictionary<string, object> message)
	{
		inputManager.ResetToDefault();
	}
}
