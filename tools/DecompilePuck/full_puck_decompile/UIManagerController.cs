using System.Collections.Generic;
using UnityEngine;

public class UIManagerController : NetworkBehaviourSingleton<UIManagerController>
{
	private UIManager uiManager;

	public override void Awake()
	{
		base.Awake();
		uiManager = GetComponent<UIManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSocialClickDiscord", Event_Client_OnSocialClickDiscord);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSocialClickPatreon", Event_Client_OnSocialClickPatreon);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnTeamSelectClickTeamBlue", Event_Client_OnTeamSelectClickTeamBlue);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnTeamSelectClickTeamRed", Event_Client_OnTeamSelectClickTeamRed);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnTeamSelectClickTeamSpectator", Event_Client_OnTeamSelectClickTeamSpectator);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnUserInterfaceScaleChanged", Event_Client_OnUserInterfaceScaleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnUserInterfaceSelect", Event_Client_OnUserInterfaceSelect);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnUserInterfaceClick", Event_Client_OnUserInterfaceClick);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnUserInterfaceNotification", Event_Client_OnUserInterfaceNotification);
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSocialClickDiscord", Event_Client_OnSocialClickDiscord);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSocialClickPatreon", Event_Client_OnSocialClickPatreon);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnTeamSelectClickTeamBlue", Event_Client_OnTeamSelectClickTeamBlue);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnTeamSelectClickTeamRed", Event_Client_OnTeamSelectClickTeamRed);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnTeamSelectClickTeamSpectator", Event_Client_OnTeamSelectClickTeamSpectator);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnUserInterfaceScaleChanged", Event_Client_OnUserInterfaceScaleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnUserInterfaceSelect", Event_Client_OnUserInterfaceSelect);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnUserInterfaceClick", Event_Client_OnUserInterfaceClick);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnUserInterfaceNotification", Event_Client_OnUserInterfaceNotification);
		base.OnDestroy();
	}

	private void Event_Client_OnSocialClickDiscord(Dictionary<string, object> message)
	{
		Application.OpenURL("https://discord.gg/AZDBj6XsGg");
	}

	private void Event_Client_OnSocialClickPatreon(Dictionary<string, object> message)
	{
		Application.OpenURL("https://www.patreon.com/c/PuckGame");
	}

	private void Event_Client_OnTeamSelectClickTeamBlue(Dictionary<string, object> message)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerSelectTeam", new Dictionary<string, object>
		{
			{
				"clientId",
				base.NetworkManager.LocalClientId
			},
			{
				"team",
				PlayerTeam.Blue
			}
		});
	}

	private void Event_Client_OnTeamSelectClickTeamRed(Dictionary<string, object> message)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerSelectTeam", new Dictionary<string, object>
		{
			{
				"clientId",
				base.NetworkManager.LocalClientId
			},
			{
				"team",
				PlayerTeam.Red
			}
		});
	}

	private void Event_Client_OnTeamSelectClickTeamSpectator(Dictionary<string, object> message)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerSelectTeam", new Dictionary<string, object>
		{
			{
				"clientId",
				base.NetworkManager.LocalClientId
			},
			{
				"team",
				PlayerTeam.Spectator
			}
		});
	}

	private void Event_Client_OnUserInterfaceScaleChanged(Dictionary<string, object> message)
	{
		float uiScale = (float)message["value"];
		uiManager.SetUiScale(uiScale);
	}

	private void Event_Client_OnUserInterfaceSelect(Dictionary<string, object> message)
	{
		uiManager.PlayerSelectSound();
	}

	private void Event_Client_OnUserInterfaceClick(Dictionary<string, object> message)
	{
		uiManager.PlayerClickSound();
	}

	private void Event_Client_OnUserInterfaceNotification(Dictionary<string, object> message)
	{
		uiManager.PlayerNotificationSound();
	}

	protected override void __initializeVariables()
	{
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		base.__initializeRpcs();
	}

	protected internal override string __getTypeName()
	{
		return "UIManagerController";
	}
}
