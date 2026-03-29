using UnityEngine.UIElements;

public class UIPauseMenu : UIComponent<UIPauseMenu>
{
	private Button switchTeamButton;

	private Button disconnectButton;

	private Button settingsButton;

	private Button exitGameButton;

	private void Start()
	{
		base.VisibilityRequiresMouse = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("PauseMenuContainer");
		switchTeamButton = container.Query<Button>("SwitchTeamButton");
		switchTeamButton.clicked += OnClickSwitchTeam;
		settingsButton = container.Query<Button>("SettingsButton");
		settingsButton.clicked += OnClickSettings;
		disconnectButton = container.Query<Button>("DisconnectButton");
		disconnectButton.clicked += OnClickDisconnect;
		exitGameButton = container.Query<Button>("ExitGameButton");
		exitGameButton.clicked += OnClickExitGame;
	}

	private void OnClickSwitchTeam()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPauseMenuClickSwitchTeam");
	}

	private void OnClickSettings()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPauseMenuClickSettings");
	}

	private void OnClickDisconnect()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPauseMenuClickDisconnect");
	}

	private void OnClickExitGame()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPauseMenuClickExitGame");
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
		return "UIPauseMenu";
	}
}
