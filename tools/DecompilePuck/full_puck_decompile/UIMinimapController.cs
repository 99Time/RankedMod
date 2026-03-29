using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

internal class UIMinimapController : NetworkBehaviour
{
	private UIMinimap uiMinimap;

	private void Awake()
	{
		uiMinimap = GetComponent<UIMinimap>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerNumberChanged", Event_OnPlayerNumberChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerBodyDespawned", Event_OnPlayerBodyDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPuckSpawned", Event_OnPuckSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPuckDespawned", Event_OnPuckDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnShowMinimapChanged", Event_Client_OnShowMinimapChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMinimapOpacityChanged", Event_Client_OnMinimapOpacityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnShowGameUserInterfaceChanged", Event_Client_OnShowGameUserInterfaceChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMinimapHorizontalPositionChanged", Event_Client_OnMinimapHorizontalPositionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMinimapVerticalPositionChanged", Event_Client_OnMinimapVerticalPositionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMinimapBackgroundOpacityChanged", Event_Client_OnMinimapBackgroundOpacityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMinimapScaleChanged", Event_Client_OnMinimapScaleChanged);
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerTeamChanged", Event_OnPlayerTeamChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerNumberChanged", Event_OnPlayerNumberChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerBodyDespawned", Event_OnPlayerBodyDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPuckSpawned", Event_OnPuckSpawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPuckDespawned", Event_OnPuckDespawned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnShowMinimapChanged", Event_Client_OnShowMinimapChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMinimapOpacityChanged", Event_Client_OnMinimapOpacityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnShowGameUserInterfaceChanged", Event_Client_OnShowGameUserInterfaceChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMinimapHorizontalPositionChanged", Event_Client_OnMinimapHorizontalPositionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMinimapVerticalPositionChanged", Event_Client_OnMinimapVerticalPositionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMinimapBackgroundOpacityChanged", Event_Client_OnMinimapBackgroundOpacityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMinimapScaleChanged", Event_Client_OnMinimapScaleChanged);
		base.OnDestroy();
	}

	private void Event_OnPlayerBodySpawned(Dictionary<string, object> message)
	{
		PlayerBodyV2 playerBody = (PlayerBodyV2)message["playerBody"];
		uiMinimap.AddPlayerBody(playerBody);
	}

	private void Event_OnPlayerTeamChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		PlayerTeam team = (PlayerTeam)message["newTeam"];
		if (player.IsLocalPlayer)
		{
			uiMinimap.Team = team;
		}
		uiMinimap.UpdatePlayerBody(player.PlayerBody);
	}

	private void Event_OnPlayerRoleChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		uiMinimap.UpdatePlayerBody(player.PlayerBody);
	}

	private void Event_OnPlayerNumberChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		uiMinimap.UpdatePlayerBody(player.PlayerBody);
	}

	private void Event_OnPlayerBodyDespawned(Dictionary<string, object> message)
	{
		PlayerBodyV2 playerBody = (PlayerBodyV2)message["playerBody"];
		uiMinimap.RemovePlayerBody(playerBody);
	}

	private void Event_OnPuckSpawned(Dictionary<string, object> message)
	{
		Puck puck = (Puck)message["puck"];
		uiMinimap.AddPuck(puck);
	}

	private void Event_OnPuckDespawned(Dictionary<string, object> message)
	{
		Puck puck = (Puck)message["puck"];
		uiMinimap.RemovePuck(puck);
	}

	private void Event_Client_OnMinimapOpacityChanged(Dictionary<string, object> message)
	{
		float opacity = (float)message["value"];
		uiMinimap.SetOpacity(opacity);
	}

	private void Event_Client_OnShowMinimapChanged(Dictionary<string, object> message)
	{
		if (NetworkBehaviourSingleton<UIManager>.Instance.UIState != UIState.MainMenu)
		{
			if ((bool)message["value"])
			{
				uiMinimap.Show();
			}
			else
			{
				uiMinimap.Hide();
			}
		}
	}

	private void Event_Client_OnShowGameUserInterfaceChanged(Dictionary<string, object> message)
	{
		if (NetworkBehaviourSingleton<UIManager>.Instance.UIState != UIState.MainMenu)
		{
			if ((bool)message["value"])
			{
				uiMinimap.Show();
			}
			else
			{
				uiMinimap.Hide();
			}
		}
	}

	private void Event_Client_OnMinimapHorizontalPositionChanged(Dictionary<string, object> message)
	{
		float x = (float)message["value"];
		uiMinimap.SetPosition(new Vector2(x, uiMinimap.Position.y));
	}

	private void Event_Client_OnMinimapVerticalPositionChanged(Dictionary<string, object> message)
	{
		float y = (float)message["value"];
		uiMinimap.SetPosition(new Vector2(uiMinimap.Position.x, y));
	}

	private void Event_Client_OnMinimapBackgroundOpacityChanged(Dictionary<string, object> message)
	{
		float backgroundOpacity = (float)message["value"];
		uiMinimap.SetBackgroundOpacity(backgroundOpacity);
	}

	private void Event_Client_OnMinimapScaleChanged(Dictionary<string, object> message)
	{
		float scale = (float)message["value"];
		uiMinimap.SetScale(scale);
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
		return "UIMinimapController";
	}
}
