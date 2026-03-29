using System.Collections.Generic;
using UnityEngine;

public class ChangingRoomPlayerController : MonoBehaviour
{
	private ChangingRoomPlayer changingRoomPlayer;

	private void Awake()
	{
		changingRoomPlayer = GetComponent<ChangingRoomPlayer>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnChangingRoomTeamChanged", Event_Client_OnChangingRoomTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnChangingRoomRoleChanged", Event_Client_OnChangingRoomRoleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnCountryChanged", Event_Client_OnCountryChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnVisorSkinChanged", Event_Client_OnVisorSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMustacheChanged", Event_Client_OnMustacheChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnBeardChanged", Event_Client_OnBeardChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnJerseySkinChanged", Event_Client_OnJerseySkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceCountryChanged", Event_Client_OnAppearanceCountryChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceVisorChanged", Event_Client_OnAppearanceVisorChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceMustacheChanged", Event_Client_OnAppearanceMustacheChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceBeardChanged", Event_Client_OnAppearanceBeardChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceJerseyChanged", Event_Client_OnAppearanceJerseyChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceTabChanged", Event_Client_OnAppearanceTabChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceShow", Event_Client_OnAppearanceShow);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceHide", Event_Client_OnAppearanceHide);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnIdentityShow", Event_Client_OnIdentityShow);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnIdentityHide", Event_Client_OnIdentityHide);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerDataChanged", Event_Client_OnPlayerDataChanged);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnChangingRoomTeamChanged", Event_Client_OnChangingRoomTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnChangingRoomRoleChanged", Event_Client_OnChangingRoomRoleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnCountryChanged", Event_Client_OnCountryChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnVisorSkinChanged", Event_Client_OnVisorSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMustacheChanged", Event_Client_OnMustacheChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnBeardChanged", Event_Client_OnBeardChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnJerseySkinChanged", Event_Client_OnJerseySkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceCountryChanged", Event_Client_OnAppearanceCountryChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceVisorChanged", Event_Client_OnAppearanceVisorChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceMustacheChanged", Event_Client_OnAppearanceMustacheChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceBeardChanged", Event_Client_OnAppearanceBeardChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceJerseyChanged", Event_Client_OnAppearanceJerseyChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceTabChanged", Event_Client_OnAppearanceTabChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceShow", Event_Client_OnAppearanceShow);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceHide", Event_Client_OnAppearanceHide);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnIdentityShow", Event_Client_OnIdentityShow);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnIdentityHide", Event_Client_OnIdentityHide);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerDataChanged", Event_Client_OnPlayerDataChanged);
	}

	private void Event_Client_OnChangingRoomTeamChanged(Dictionary<string, object> message)
	{
		PlayerTeam team = (PlayerTeam)message["team"];
		changingRoomPlayer.Team = team;
	}

	private void Event_Client_OnChangingRoomRoleChanged(Dictionary<string, object> message)
	{
		PlayerRole role = (PlayerRole)message["role"];
		changingRoomPlayer.Role = role;
	}

	private void Event_Client_OnCountryChanged(Dictionary<string, object> message)
	{
		changingRoomPlayer.UpdatePlayerMesh();
	}

	private void Event_Client_OnVisorSkinChanged(Dictionary<string, object> message)
	{
		changingRoomPlayer.UpdatePlayerMesh();
	}

	private void Event_Client_OnMustacheChanged(Dictionary<string, object> message)
	{
		changingRoomPlayer.UpdatePlayerMesh();
	}

	private void Event_Client_OnBeardChanged(Dictionary<string, object> message)
	{
		changingRoomPlayer.UpdatePlayerMesh();
	}

	private void Event_Client_OnJerseySkinChanged(Dictionary<string, object> message)
	{
		changingRoomPlayer.UpdatePlayerMesh();
	}

	private void Event_Client_OnAppearanceCountryChanged(Dictionary<string, object> message)
	{
		string helmetFlag = (string)message["value"];
		changingRoomPlayer.PlayerMesh.PlayerHead.SetHelmetFlag(helmetFlag);
	}

	private void Event_Client_OnAppearanceVisorChanged(Dictionary<string, object> message)
	{
		string helmetVisor = (string)message["value"];
		changingRoomPlayer.PlayerMesh.PlayerHead.SetHelmetVisor(helmetVisor);
	}

	private void Event_Client_OnAppearanceMustacheChanged(Dictionary<string, object> message)
	{
		string mustache = (string)message["value"];
		changingRoomPlayer.PlayerMesh.PlayerHead.SetMustache(mustache);
	}

	private void Event_Client_OnAppearanceBeardChanged(Dictionary<string, object> message)
	{
		string beard = (string)message["value"];
		changingRoomPlayer.PlayerMesh.PlayerHead.SetBeard(beard);
	}

	private void Event_Client_OnAppearanceJerseyChanged(Dictionary<string, object> message)
	{
		string jersey = (string)message["value"];
		PlayerTeam team = (PlayerTeam)message["team"];
		changingRoomPlayer.PlayerMesh.SetJersey(team, jersey);
	}

	private void Event_Client_OnAppearanceTabChanged(Dictionary<string, object> message)
	{
		string text = (string)message["tabName"];
		if (text == "HeadTab" || text == "BodyTab")
		{
			changingRoomPlayer.RotateWithMouse = true;
			return;
		}
		changingRoomPlayer.RotateWithMouse = false;
		changingRoomPlayer.Client_MovePlayerToDefaultPosition();
	}

	private void Event_Client_OnAppearanceShow(Dictionary<string, object> message)
	{
		changingRoomPlayer.RotateWithMouse = true;
	}

	private void Event_Client_OnAppearanceHide(Dictionary<string, object> message)
	{
		changingRoomPlayer.RotateWithMouse = false;
		changingRoomPlayer.Client_MovePlayerToDefaultPosition();
	}

	private void Event_Client_OnIdentityShow(Dictionary<string, object> message)
	{
		changingRoomPlayer.RotateWithMouse = true;
		changingRoomPlayer.Client_MovePlayerToIdentityPosition();
	}

	private void Event_Client_OnIdentityHide(Dictionary<string, object> message)
	{
		changingRoomPlayer.RotateWithMouse = false;
		changingRoomPlayer.Client_MovePlayerToDefaultPosition();
	}

	private void Event_Client_OnPlayerDataChanged(Dictionary<string, object> message)
	{
		changingRoomPlayer.UpdatePlayerMesh();
	}
}
