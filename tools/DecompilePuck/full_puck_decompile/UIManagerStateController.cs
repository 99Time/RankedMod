using System.Collections.Generic;
using Unity.Netcode;

public class UIManagerStateController : NetworkBehaviour
{
	private UIManager uiManager;

	private void Awake()
	{
		uiManager = GetComponent<UIManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnPlayerStateChanged", Event_OnPlayerStateChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMainMenuClickServerBrowser", Event_Client_OnMainMenuClickServerBrowser);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMainMenuClickPlayer", Event_Client_OnMainMenuClickPlayer);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMainMenuClickSettings", Event_Client_OnMainMenuClickSettings);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMainMenuClickMods", Event_Client_OnMainMenuClickMods);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerMenuClickBack", Event_Client_OnPlayerMenuClickBack);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerMenuClickIdentity", Event_Client_OnPlayerMenuClickIdentity);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerMenuClickAppearance", Event_Client_OnPlayerMenuClickAppearance);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnIdentityClickClose", Event_Client_OnIdentityClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceClickClose", Event_Client_OnAppearanceClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPauseMenuClickSettings", Event_Client_OnPauseMenuClickSettings);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPauseMenuClickSwitchTeam", Event_Client_OnPauseMenuClickSwitchTeam);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnServerBrowserClickClose", Event_Client_OnServerBrowserClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnServerBrowserClickServer", Event_Client_OnServerBrowserClickServer);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnServerBrowserClickServerLauncher", Event_Client_OnServerBrowserClickServerLauncher);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnTeamSelectClickTeamBlue", Event_Client_OnTeamSelectClickTeamBlue);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnTeamSelectClickTeamRed", Event_Client_OnTeamSelectClickTeamRed);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnTeamSelectClickTeamSpectator", Event_Client_OnTeamSelectClickTeamSpectator);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsClickClose", Event_Client_OnSettingsClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnServerLauncherClickClose", Event_Client_OnServerLauncherClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnModsClickClose", Event_Client_OnModsClickClose);
		uiManager.SetUiState(UIState.MainMenu);
	}

	public override void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnPlayerStateChanged", Event_OnPlayerStateChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMainMenuClickServerBrowser", Event_Client_OnMainMenuClickServerBrowser);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMainMenuClickPlayer", Event_Client_OnMainMenuClickPlayer);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMainMenuClickSettings", Event_Client_OnMainMenuClickSettings);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMainMenuClickMods", Event_Client_OnMainMenuClickMods);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerMenuClickBack", Event_Client_OnPlayerMenuClickBack);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerMenuClickIdentity", Event_Client_OnPlayerMenuClickIdentity);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerMenuClickAppearance", Event_Client_OnPlayerMenuClickAppearance);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnIdentityClickClose", Event_Client_OnIdentityClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceClickClose", Event_Client_OnAppearanceClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPauseMenuClickSettings", Event_Client_OnPauseMenuClickSettings);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPauseMenuClickSwitchTeam", Event_Client_OnPauseMenuClickSwitchTeam);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnServerBrowserClickClose", Event_Client_OnServerBrowserClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnServerBrowserClickServer", Event_Client_OnServerBrowserClickServer);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnServerBrowserClickServerLauncher", Event_Client_OnServerBrowserClickServerLauncher);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnTeamSelectClickTeamBlue", Event_Client_OnTeamSelectClickTeamBlue);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnTeamSelectClickTeamRed", Event_Client_OnTeamSelectClickTeamRed);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnTeamSelectClickTeamSpectator", Event_Client_OnTeamSelectClickTeamSpectator);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsClickClose", Event_Client_OnSettingsClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnServerLauncherClickClose", Event_Client_OnServerLauncherClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnModsClickClose", Event_Client_OnModsClickClose);
		base.OnDestroy();
	}

	private void Event_OnPlayerStateChanged(Dictionary<string, object> message)
	{
		Player player = (Player)message["player"];
		PlayerState playerState = (PlayerState)message["oldState"];
		if (player.IsLocalPlayer)
		{
			if (playerState == PlayerState.None)
			{
				uiManager.SetUiState(UIState.Play);
			}
			switch (player.State.Value)
			{
			case PlayerState.TeamSelect:
				uiManager.PositionSelect.Hide();
				uiManager.TeamSelect.Show();
				break;
			case PlayerState.PositionSelectBlue:
			case PlayerState.PositionSelectRed:
				uiManager.TeamSelect.Hide();
				uiManager.PositionSelect.Show();
				break;
			default:
				uiManager.TeamSelect.Hide();
				uiManager.PositionSelect.Hide();
				break;
			}
		}
	}

	private void Event_Client_OnClientStopped(Dictionary<string, object> message)
	{
		uiManager.SetUiState(UIState.MainMenu);
	}

	private void Event_Client_OnMainMenuClickServerBrowser(Dictionary<string, object> message)
	{
		uiManager.MainMenu.Hide();
		uiManager.ServerBrowser.Show();
	}

	private void Event_Client_OnMainMenuClickPlayer(Dictionary<string, object> message)
	{
		uiManager.MainMenu.Hide();
		uiManager.PlayerMenu.Show();
	}

	private void Event_Client_OnMainMenuClickSettings(Dictionary<string, object> message)
	{
		uiManager.MainMenu.Hide();
		uiManager.Settings.Show();
	}

	private void Event_Client_OnMainMenuClickMods(Dictionary<string, object> message)
	{
		uiManager.MainMenu.Hide();
		uiManager.Mods.Show();
	}

	private void Event_Client_OnPlayerMenuClickBack(Dictionary<string, object> message)
	{
		uiManager.MainMenu.Show();
		uiManager.PlayerMenu.Hide();
	}

	private void Event_Client_OnPlayerMenuClickIdentity(Dictionary<string, object> message)
	{
		uiManager.PlayerMenu.Hide();
		uiManager.Identity.Show();
	}

	private void Event_Client_OnPlayerMenuClickAppearance(Dictionary<string, object> message)
	{
		uiManager.PlayerMenu.Hide();
		uiManager.Appearance.Show();
	}

	private void Event_Client_OnIdentityClickClose(Dictionary<string, object> message)
	{
		uiManager.PlayerMenu.Show();
		uiManager.Identity.Hide();
	}

	private void Event_Client_OnAppearanceClickClose(Dictionary<string, object> message)
	{
		uiManager.PlayerMenu.Show();
		uiManager.Appearance.Hide();
	}

	private void Event_Client_OnPauseMenuClickSettings(Dictionary<string, object> message)
	{
		uiManager.Settings.Show();
		uiManager.PauseMenu.Hide();
	}

	private void Event_Client_OnPauseMenuClickSwitchTeam(Dictionary<string, object> message)
	{
		uiManager.PauseMenu.Hide();
	}

	private void Event_Client_OnServerBrowserClickClose(Dictionary<string, object> message)
	{
		uiManager.ServerBrowser.Hide();
		uiManager.MainMenu.Show();
	}

	private void Event_Client_OnServerBrowserClickServer(Dictionary<string, object> message)
	{
		uiManager.ServerBrowser.Hide();
	}

	private void Event_Client_OnServerBrowserClickServerLauncher(Dictionary<string, object> message)
	{
		uiManager.ServerBrowser.Hide();
		uiManager.ServerLauncher.Show();
	}

	private void Event_Client_OnTeamSelectClickTeamBlue(Dictionary<string, object> message)
	{
		uiManager.TeamSelect.Hide();
	}

	private void Event_Client_OnTeamSelectClickTeamRed(Dictionary<string, object> message)
	{
		uiManager.TeamSelect.Hide();
	}

	private void Event_Client_OnTeamSelectClickTeamSpectator(Dictionary<string, object> message)
	{
		uiManager.TeamSelect.Hide();
	}

	private void Event_Client_OnSettingsClickClose(Dictionary<string, object> message)
	{
		uiManager.Settings.Hide();
		if (uiManager.UIState == UIState.MainMenu)
		{
			uiManager.MainMenu.Show();
		}
		else
		{
			uiManager.PauseMenu.Show();
		}
	}

	private void Event_Client_OnServerLauncherClickClose(Dictionary<string, object> message)
	{
		uiManager.ServerLauncher.Hide();
		uiManager.ServerBrowser.Show();
	}

	private void Event_Client_OnModsClickClose(Dictionary<string, object> message)
	{
		uiManager.Mods.Hide();
		uiManager.MainMenu.Show();
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
		return "UIManagerStateController";
	}
}
