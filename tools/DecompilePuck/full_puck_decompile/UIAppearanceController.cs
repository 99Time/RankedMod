using System.Collections.Generic;
using UnityEngine;

public class UIAppearanceController : MonoBehaviour
{
	private UIAppearance uiAppearance;

	private void Awake()
	{
		uiAppearance = GetComponent<UIAppearance>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnChangingRoomTeamChanged", Event_Client_OnChangingRoomTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnChangingRoomRoleChanged", Event_Client_OnChangingRoomRoleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceClickClose", Event_Client_OnAppearanceClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnOwnedItemIdsUpdated", Event_Client_OnOwnedItemIdsUpdated);
		uiAppearance.ApplyAppearanceValues();
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnChangingRoomTeamChanged", Event_Client_OnChangingRoomTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnChangingRoomRoleChanged", Event_Client_OnChangingRoomRoleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceClickClose", Event_Client_OnAppearanceClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnOwnedItemIdsUpdated", Event_Client_OnOwnedItemIdsUpdated);
	}

	private void Event_Client_OnChangingRoomTeamChanged(Dictionary<string, object> message)
	{
		PlayerTeam team = (PlayerTeam)message["team"];
		uiAppearance.Team = team;
		uiAppearance.Reload();
		uiAppearance.ApplyAppearanceValues();
	}

	private void Event_Client_OnChangingRoomRoleChanged(Dictionary<string, object> message)
	{
		PlayerRole role = (PlayerRole)message["role"];
		uiAppearance.Role = role;
		uiAppearance.Reload();
		uiAppearance.ApplyAppearanceValues();
	}

	private void Event_Client_OnAppearanceClickClose(Dictionary<string, object> message)
	{
		uiAppearance.Reload();
		uiAppearance.ApplyAppearanceValues();
	}

	private void Event_Client_OnOwnedItemIdsUpdated(Dictionary<string, object> message)
	{
		int[] ownedItemIds = (int[])message["ownedItemIds"];
		uiAppearance.SetOwnedItemIds(ownedItemIds);
	}
}
