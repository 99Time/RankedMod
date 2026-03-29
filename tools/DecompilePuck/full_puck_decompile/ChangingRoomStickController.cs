using System.Collections.Generic;
using UnityEngine;

public class ChangingRoomStickController : MonoBehaviour
{
	private ChangingRoomStick changingRoomStick;

	private void Awake()
	{
		changingRoomStick = GetComponent<ChangingRoomStick>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnChangingRoomTeamChanged", Event_Client_OnChangingRoomTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnChangingRoomRoleChanged", Event_Client_OnChangingRoomRoleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnStickSkinChanged", Event_Client_OnStickSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnStickShaftTapeSkinChanged", Event_Client_OnStickShaftTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnStickBladeTapeSkinChanged", Event_Client_OnStickBladeTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceStickSkinChanged", Event_Client_OnAppearanceStickSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceStickShaftTapeSkinChanged", Event_Client_OnAppearanceStickShaftTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceStickBladeTapeSkinChanged", Event_Client_OnAppearanceStickBladeTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceHide", Event_Client_OnAppearanceHide);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceTabChanged", Event_Client_OnAppearanceTabChanged);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnChangingRoomTeamChanged", Event_Client_OnChangingRoomTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnChangingRoomRoleChanged", Event_Client_OnChangingRoomRoleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnStickSkinChanged", Event_Client_OnStickSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnStickShaftTapeSkinChanged", Event_Client_OnStickShaftTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnStickBladeTapeSkinChanged", Event_Client_OnStickBladeTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceStickSkinChanged", Event_Client_OnAppearanceStickSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceStickShaftTapeSkinChanged", Event_Client_OnAppearanceStickShaftTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceStickBladeTapeSkinChanged", Event_Client_OnAppearanceStickBladeTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceHide", Event_Client_OnAppearanceHide);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceTabChanged", Event_Client_OnAppearanceTabChanged);
	}

	private void Event_Client_OnChangingRoomTeamChanged(Dictionary<string, object> message)
	{
		PlayerTeam team = (PlayerTeam)message["team"];
		changingRoomStick.Team = team;
	}

	private void Event_Client_OnChangingRoomRoleChanged(Dictionary<string, object> message)
	{
		PlayerRole playerRole = (PlayerRole)message["role"];
		if (changingRoomStick.Role != playerRole)
		{
			changingRoomStick.Hide();
		}
		else
		{
			changingRoomStick.Show();
		}
	}

	private void Event_Client_OnStickSkinChanged(Dictionary<string, object> message)
	{
		changingRoomStick.UpdateStickMesh();
	}

	private void Event_Client_OnStickShaftTapeSkinChanged(Dictionary<string, object> message)
	{
		changingRoomStick.UpdateStickMesh();
	}

	private void Event_Client_OnStickBladeTapeSkinChanged(Dictionary<string, object> message)
	{
		changingRoomStick.UpdateStickMesh();
	}

	private void Event_Client_OnAppearanceStickSkinChanged(Dictionary<string, object> message)
	{
		PlayerTeam team = (PlayerTeam)message["team"];
		string skinName = (string)message["value"];
		changingRoomStick.StickMesh.SetSkin(team, skinName);
	}

	private void Event_Client_OnAppearanceStickShaftTapeSkinChanged(Dictionary<string, object> message)
	{
		string shaftTape = (string)message["value"];
		changingRoomStick.StickMesh.SetShaftTape(shaftTape);
	}

	private void Event_Client_OnAppearanceStickBladeTapeSkinChanged(Dictionary<string, object> message)
	{
		string bladeTape = (string)message["value"];
		changingRoomStick.StickMesh.SetBladeTape(bladeTape);
	}

	private void Event_Client_OnAppearanceTabChanged(Dictionary<string, object> message)
	{
		if ((string)message["tabName"] == "StickTab")
		{
			changingRoomStick.Client_MoveStickToAppearanceStickPosition();
			changingRoomStick.RotateWithMouse = true;
		}
		else
		{
			changingRoomStick.RotateWithMouse = false;
			changingRoomStick.Client_MoveStickToDefaultPosition();
		}
	}

	private void Event_Client_OnAppearanceHide(Dictionary<string, object> message)
	{
		changingRoomStick.RotateWithMouse = false;
		changingRoomStick.Client_MoveStickToDefaultPosition();
	}
}
