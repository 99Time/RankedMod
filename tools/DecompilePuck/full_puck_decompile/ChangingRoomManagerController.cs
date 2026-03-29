using System.Collections.Generic;
using UnityEngine;

public class ChangingRoomManagerController : MonoBehaviour
{
	private ChangingRoomManager changingRoomManager;

	private void Awake()
	{
		changingRoomManager = GetComponent<ChangingRoomManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnChangingRoomReady", Event_Client_OnChangingRoomReady);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMainMenuShow", Event_Client_OnMainMenuShow);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerMenuShow", Event_Client_OnPlayerMenuShow);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnIdentityShow", Event_Client_OnIdentityShow);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceShow", Event_Client_OnAppearanceShow);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceTabChanged", Event_Client_OnAppearanceTabChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceTeamChanged", Event_Client_OnAppearanceTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceRoleChanged", Event_Client_OnAppearanceRoleChanged);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnChangingRoomReady", Event_Client_OnChangingRoomReady);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMainMenuShow", Event_Client_OnMainMenuShow);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerMenuShow", Event_Client_OnPlayerMenuShow);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnIdentityShow", Event_Client_OnIdentityShow);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceShow", Event_Client_OnAppearanceShow);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceTabChanged", Event_Client_OnAppearanceTabChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceTeamChanged", Event_Client_OnAppearanceTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceRoleChanged", Event_Client_OnAppearanceRoleChanged);
	}

	private void Event_Client_OnChangingRoomReady(Dictionary<string, object> message)
	{
		changingRoomManager.Client_EnableMainCamera();
		changingRoomManager.Team = PlayerTeam.Blue;
		changingRoomManager.Role = PlayerRole.Attacker;
	}

	private void Event_Client_OnMainMenuShow(Dictionary<string, object> message)
	{
		changingRoomManager.Client_MoveCameraToDefaultPosition();
	}

	private void Event_Client_OnPlayerMenuShow(Dictionary<string, object> message)
	{
		changingRoomManager.Client_MoveCameraToPlayerPosition();
	}

	private void Event_Client_OnIdentityShow(Dictionary<string, object> message)
	{
		changingRoomManager.Client_MoveCameraToIdentityPosition();
	}

	private void Event_Client_OnAppearanceShow(Dictionary<string, object> message)
	{
		changingRoomManager.Client_MoveCameraToAppearanceDefaultPosition();
	}

	private void Event_Client_OnAppearanceTabChanged(Dictionary<string, object> message)
	{
		if ((string)message["tabName"] == "HeadTab")
		{
			changingRoomManager.Client_MoveCameraToAppearanceHeadPosition();
		}
		else
		{
			changingRoomManager.Client_MoveCameraToAppearanceDefaultPosition();
		}
	}

	private void Event_Client_OnAppearanceTeamChanged(Dictionary<string, object> message)
	{
		PlayerTeam team = (PlayerTeam)message["team"];
		changingRoomManager.Team = team;
	}

	private void Event_Client_OnAppearanceRoleChanged(Dictionary<string, object> message)
	{
		PlayerRole role = (PlayerRole)message["role"];
		changingRoomManager.Role = role;
	}
}
