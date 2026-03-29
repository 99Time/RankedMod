using System.Collections.Generic;
using UnityEngine.UIElements;

public class UIMainMenu : UIComponent<UIMainMenu>
{
	private VisualElement debugToolsContainer;

	private TextField ipTextField;

	private TextField portTextField;

	private TextField passwordTextField;

	private Button hostServerButton;

	private Button joinServerButton;

	private Button practiceButton;

	private Button serverBrowserButton;

	private Button playerButton;

	private Button settingsButton;

	private Button modsButton;

	private Button exitGameButton;

	private VisualElement socialContainer;

	private Button discordButton;

	private Button patreonButton;

	private string ip = "127.0.0.1";

	private ushort port = 7777;

	private string password = "";

	private void Start()
	{
		base.VisibilityRequiresMouse = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("MainMenuContainer");
		debugToolsContainer = container.Query<VisualElement>("DebugToolsContainer");
		ipTextField = container.Query<VisualElement>("IpTextField").First().Query<TextField>("TextField");
		ipTextField.RegisterValueChangedCallback(OnIpChanged);
		ipTextField.value = ip;
		portTextField = container.Query<VisualElement>("PortTextField").First().Query<TextField>("TextField");
		portTextField.RegisterValueChangedCallback(OnPortChanged);
		portTextField.value = port.ToString();
		passwordTextField = container.Query<VisualElement>("PasswordTextField").First().Query<TextField>("TextField");
		passwordTextField.RegisterValueChangedCallback(OnPasswordChanged);
		passwordTextField.value = password;
		hostServerButton = container.Query<Button>("HostServerButton");
		hostServerButton.clicked += OnClickHostServer;
		joinServerButton = container.Query<Button>("JoinServerButton");
		joinServerButton.clicked += OnClickJoinServer;
		practiceButton = container.Query<Button>("PracticeButton");
		practiceButton.clicked += OnClickPractice;
		serverBrowserButton = container.Query<Button>("ServerBrowserButton");
		serverBrowserButton.clicked += OnClickServerBrowser;
		playerButton = container.Query<Button>("PlayerButton");
		playerButton.clicked += OnClickPlayer;
		settingsButton = container.Query<Button>("SettingsButton");
		settingsButton.clicked += OnClickSettings;
		modsButton = container.Query<Button>("ModsButton");
		modsButton.clicked += OnClickMods;
		exitGameButton = container.Query<Button>("ExitGameButton");
		exitGameButton.clicked += OnClickExitGame;
		socialContainer = rootVisualElement.Query<VisualElement>("SocialContainer");
		discordButton = socialContainer.Query<Button>("DiscordButton");
		discordButton.clicked += OnClickDiscord;
		patreonButton = socialContainer.Query<Button>("PatreonButton");
		patreonButton.clicked += OnClickPatreon;
	}

	public override void Show()
	{
		base.Show();
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMainMenuShow");
		if (socialContainer != null)
		{
			socialContainer.style.display = DisplayStyle.Flex;
		}
	}

	public override void Hide(bool ignoreAlwaysVisible = false)
	{
		base.Hide(ignoreAlwaysVisible);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMainMenuHide");
		if (socialContainer != null)
		{
			socialContainer.style.display = DisplayStyle.None;
		}
	}

	public void ShowDebugTools()
	{
		debugToolsContainer.style.display = DisplayStyle.Flex;
	}

	public void HideDebugTools()
	{
		debugToolsContainer.style.display = DisplayStyle.None;
	}

	private void OnIpChanged(ChangeEvent<string> changeEvent)
	{
		ip = changeEvent.newValue;
	}

	private void OnPortChanged(ChangeEvent<string> changeEvent)
	{
		if (ushort.TryParse(changeEvent.newValue, out var result))
		{
			port = result;
		}
	}

	private void OnPasswordChanged(ChangeEvent<string> changeEvent)
	{
		password = changeEvent.newValue;
	}

	private void OnClickHostServer()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMainMenuClickHostServer", new Dictionary<string, object>
		{
			{ "port", port },
			{ "password", password }
		});
	}

	private void OnClickJoinServer()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMainMenuClickJoinServer", new Dictionary<string, object>
		{
			{ "ip", ip },
			{ "port", port },
			{ "password", password }
		});
	}

	private void OnClickPractice()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMainMenuClickPractice");
	}

	private void OnClickServerBrowser()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMainMenuClickServerBrowser");
	}

	private void OnClickPlayer()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMainMenuClickPlayer");
	}

	private void OnClickSettings()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMainMenuClickSettings");
	}

	private void OnClickMods()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMainMenuClickMods");
	}

	private void OnClickExitGame()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMainMenuClickExitGame");
	}

	private void OnClickDiscord()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSocialClickDiscord");
	}

	private void OnClickPatreon()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSocialClickPatreon");
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
		return "UIMainMenu";
	}
}
