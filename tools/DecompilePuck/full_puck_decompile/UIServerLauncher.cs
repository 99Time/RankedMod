using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class UIServerLauncher : UIComponent<UIServerLauncher>
{
	private Button closeButton;

	private Button startButton;

	private TabView tabView;

	private Tab dedicatedTab;

	private Tab selfHostedTab;

	private TextField dedicatedNameTextField;

	private DropdownField dedicatedLocationDropdown;

	private Slider dedicatedMaxPlayerSlider;

	private Toggle dedicatedPasswordProtectedToggle;

	private TextField dedicatedPasswordTextField;

	private VisualElement dedicatedPasswordProtectionCoverVisualElement;

	private TextField selfHostedNameTextField;

	private IntegerField selfHostedPortIntegerField;

	private Slider selfHostedMaxPlayerSlider;

	private Toggle selfHostedPasswordProtectedToggle;

	private TextField selfHostedPasswordTextField;

	private Toggle selfHostedVoipToggle;

	private ServerLauncherLocation[] dedicatedLauncherLocations = new ServerLauncherLocation[0];

	private string dedicatedName = "MY DEDICATED PUCK SERVER";

	private ServerLauncherLocation dedicatedLocation;

	private int dedicatedMaxPlayers = 6;

	private string dedicatedPassword = "";

	private int selfHostedPort = 7777;

	private string selfHostedName = "MY SELF HOSTED PUCK SERVER";

	private int selfHostedMaxPlayers = 12;

	private string selfHostedPassword = "";

	private bool selfHostedVoip = true;

	private void Start()
	{
		base.VisibilityRequiresMouse = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("ServerLauncherContainer");
		closeButton = container.Query<Button>("CloseButton");
		closeButton.clicked += OnClickClose;
		startButton = container.Query<Button>("StartButton");
		startButton.clicked += OnClickLaunch;
		tabView = container.Query<TabView>("TabView");
		dedicatedTab = container.Query<Tab>("DedicatedTab");
		dedicatedNameTextField = dedicatedTab.Query<VisualElement>("NameTextField").First().Query<TextField>("TextField");
		dedicatedNameTextField.RegisterValueChangedCallback(OnDedicatedNameChanged);
		dedicatedNameTextField.RegisterCallback<FocusOutEvent>(OnDedicatedNameFocusOut);
		dedicatedNameTextField.value = dedicatedName;
		dedicatedLocationDropdown = dedicatedTab.Query<VisualElement>("LocationDropdown").First().Query<DropdownField>("Dropdown");
		dedicatedLocationDropdown.RegisterValueChangedCallback(OnDedicatedLocationChanged);
		dedicatedMaxPlayerSlider = dedicatedTab.Query<VisualElement>("MaxPlayersSlider").First().Query<Slider>("Slider");
		dedicatedMaxPlayerSlider.RegisterValueChangedCallback(OnDedicatedMaxPlayersChanged);
		dedicatedMaxPlayerSlider.value = dedicatedMaxPlayers;
		dedicatedPasswordProtectedToggle = dedicatedTab.Query<VisualElement>("PasswordProtectedToggle").First().Query<Toggle>("Toggle");
		dedicatedPasswordProtectedToggle.RegisterValueChangedCallback(OnDedicatedPasswordProtectedChanged);
		dedicatedPasswordProtectedToggle.value = !string.IsNullOrEmpty(dedicatedPassword);
		dedicatedPasswordTextField = dedicatedTab.Query<VisualElement>("PasswordTextField").First().Query<TextField>("TextField");
		dedicatedPasswordTextField.RegisterValueChangedCallback(OnDedicatedPasswordChanged);
		dedicatedPasswordTextField.SetEnabled(dedicatedPasswordProtectedToggle.value);
		dedicatedPasswordTextField.value = dedicatedPassword;
		dedicatedPasswordProtectionCoverVisualElement = dedicatedTab.Query<VisualElement>("PasswordProtectionCover").First();
		selfHostedTab = container.Query<Tab>("SelfHostedTab");
		selfHostedNameTextField = selfHostedTab.Query<VisualElement>("NameTextField").First().Query<TextField>("TextField");
		selfHostedNameTextField.RegisterValueChangedCallback(OnSelfHostedNameChanged);
		selfHostedNameTextField.RegisterCallback<FocusOutEvent>(OnSelfHostedNameFocusOut);
		selfHostedNameTextField.value = selfHostedName;
		selfHostedPortIntegerField = selfHostedTab.Query<VisualElement>("PortIntegerField").First().Query<IntegerField>("IntegerField");
		selfHostedPortIntegerField.RegisterValueChangedCallback(OnSelfHostedPortChanged);
		selfHostedPortIntegerField.value = selfHostedPort;
		selfHostedMaxPlayerSlider = selfHostedTab.Query<VisualElement>("MaxPlayersSlider").First().Query<Slider>("Slider");
		selfHostedMaxPlayerSlider.RegisterValueChangedCallback(OnSelfHostedMaxPlayersChanged);
		selfHostedMaxPlayerSlider.value = selfHostedMaxPlayers;
		selfHostedPasswordProtectedToggle = selfHostedTab.Query<VisualElement>("PasswordProtectedToggle").First().Query<Toggle>("Toggle");
		selfHostedPasswordProtectedToggle.RegisterValueChangedCallback(OnSelfHostedPasswordProtectedChanged);
		selfHostedPasswordProtectedToggle.value = !string.IsNullOrEmpty(selfHostedPassword);
		selfHostedPasswordTextField = selfHostedTab.Query<VisualElement>("PasswordTextField").First().Query<TextField>("TextField");
		selfHostedPasswordTextField.RegisterValueChangedCallback(OnSelfHostedPasswordChanged);
		selfHostedPasswordTextField.SetEnabled(selfHostedPasswordProtectedToggle.value);
		selfHostedPasswordTextField.value = selfHostedPassword;
		selfHostedVoipToggle = selfHostedTab.Query<VisualElement>("VoipToggle").First().Query<Toggle>("Toggle");
		selfHostedVoipToggle.RegisterValueChangedCallback(OnSelfHostedVoipChanged);
		selfHostedVoipToggle.value = selfHostedVoip;
	}

	public override void Show()
	{
		if (!base.IsVisible)
		{
			Refresh();
		}
		base.Show();
	}

	public void Refresh()
	{
		startButton.SetEnabled(value: false);
		dedicatedLauncherLocations = new ServerLauncherLocation[0];
		dedicatedLocationDropdown.choices = new List<string> { "LOADING..." };
		dedicatedLocationDropdown.value = dedicatedLocationDropdown.choices.First();
		MonoBehaviourSingleton<WebSocketManager>.Instance.Emit("playerGetServerLauncherLocationsRequest", null, "playerGetServerLauncherLocationsResponse");
	}

	public void LaunchDedicatedServer()
	{
		MonoBehaviourSingleton<WebSocketManager>.Instance.Emit("playerLaunchServerRequest", new Dictionary<string, object>
		{
			{ "name", dedicatedName },
			{ "maxPlayers", dedicatedMaxPlayers },
			{ "password", dedicatedPassword },
			{ "location", dedicatedLocation }
		}, "playerLaunchServerResponse");
	}

	public void LaunchSelfHostedServer()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnServerLauncherClickStartSelfHostedServer", new Dictionary<string, object>
		{
			{ "port", selfHostedPort },
			{ "name", selfHostedName },
			{ "maxPlayers", selfHostedMaxPlayers },
			{ "password", selfHostedPassword },
			{ "voip", selfHostedVoip }
		});
	}

	public void HideDedicatedPasswordProtection()
	{
		dedicatedPasswordProtectedToggle.SetEnabled(value: false);
		dedicatedPasswordTextField.SetEnabled(value: false);
		dedicatedPasswordProtectionCoverVisualElement.visible = true;
	}

	public void ShowDedicatedPasswordProtection()
	{
		dedicatedPasswordProtectedToggle.SetEnabled(value: true);
		dedicatedPasswordProtectedToggle.value = false;
		dedicatedPasswordTextField.SetEnabled(value: false);
		dedicatedPasswordTextField.value = "";
		dedicatedPasswordProtectionCoverVisualElement.visible = false;
	}

	public void SetDedicatedLocations(ServerLauncherLocation[] locations)
	{
		dedicatedLauncherLocations = locations;
		List<string> list = dedicatedLauncherLocations.Select((ServerLauncherLocation location) => (location.continent + ": " + location.city).ToUpper()).ToList();
		list.Sort();
		dedicatedLocationDropdown.choices = list;
		dedicatedLocationDropdown.value = dedicatedLocationDropdown.choices.First();
		startButton.SetEnabled(value: true);
	}

	private void ResetDedicatedName()
	{
		dedicatedName = "MY PUCK SERVER";
		dedicatedNameTextField.value = dedicatedName;
	}

	private void ResetSelfHostedName()
	{
		selfHostedName = "MY PUCK SERVER";
		selfHostedNameTextField.value = selfHostedName;
	}

	private void OnClickClose()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnServerLauncherClickClose");
	}

	private void OnClickLaunch()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnServerLauncherClickClose");
		if (tabView.activeTab == dedicatedTab)
		{
			LaunchDedicatedServer();
		}
		else if (tabView.activeTab == selfHostedTab)
		{
			LaunchSelfHostedServer();
		}
	}

	private void OnDedicatedNameChanged(ChangeEvent<string> changeEvent)
	{
		dedicatedName = Utils.FilterStringSpecialCharacters(changeEvent.newValue);
		dedicatedNameTextField.value = dedicatedName;
	}

	private void OnDedicatedNameFocusOut(FocusOutEvent focusOutEvent)
	{
		dedicatedName = Utils.FilterStringSpecialCharacters(dedicatedName);
		dedicatedName = Utils.FilterStringProfanity(dedicatedName);
		if (string.IsNullOrEmpty(dedicatedName))
		{
			ResetDedicatedName();
		}
		else
		{
			dedicatedNameTextField.value = dedicatedName;
		}
	}

	private void OnDedicatedLocationChanged(ChangeEvent<string> changeEvent)
	{
		ServerLauncherLocation serverLauncherLocation = dedicatedLauncherLocations.FirstOrDefault((ServerLauncherLocation location) => (location.continent + ": " + location.city).ToUpper() == changeEvent.newValue);
		if (serverLauncherLocation != null)
		{
			dedicatedLocation = serverLauncherLocation;
		}
	}

	private void OnDedicatedMaxPlayersChanged(ChangeEvent<float> changeEvent)
	{
		dedicatedMaxPlayers = Mathf.RoundToInt(changeEvent.newValue);
		dedicatedMaxPlayerSlider.value = dedicatedMaxPlayers;
	}

	private void OnDedicatedPasswordProtectedChanged(ChangeEvent<bool> changeEvent)
	{
		if (changeEvent.newValue)
		{
			dedicatedPasswordTextField.SetEnabled(value: true);
			return;
		}
		dedicatedPasswordTextField.SetEnabled(value: false);
		dedicatedPasswordTextField.value = "";
	}

	private void OnDedicatedPasswordChanged(ChangeEvent<string> changeEvent)
	{
		dedicatedPassword = changeEvent.newValue;
		dedicatedPasswordTextField.value = dedicatedPassword;
	}

	private void OnSelfHostedNameChanged(ChangeEvent<string> changeEvent)
	{
		selfHostedName = Utils.FilterStringSpecialCharacters(changeEvent.newValue);
		selfHostedNameTextField.value = selfHostedName;
	}

	private void OnSelfHostedNameFocusOut(FocusOutEvent focusOutEvent)
	{
		selfHostedName = Utils.FilterStringSpecialCharacters(selfHostedName);
		selfHostedName = Utils.FilterStringProfanity(selfHostedName);
		if (string.IsNullOrEmpty(selfHostedName))
		{
			ResetSelfHostedName();
		}
		else
		{
			selfHostedNameTextField.value = selfHostedName;
		}
	}

	private void OnSelfHostedPortChanged(ChangeEvent<int> changeEvent)
	{
		selfHostedPort = changeEvent.newValue;
		selfHostedPortIntegerField.value = selfHostedPort;
	}

	private void OnSelfHostedMaxPlayersChanged(ChangeEvent<float> changeEvent)
	{
		selfHostedMaxPlayers = Mathf.RoundToInt(changeEvent.newValue);
		selfHostedMaxPlayerSlider.value = selfHostedMaxPlayers;
	}

	private void OnSelfHostedPasswordProtectedChanged(ChangeEvent<bool> changeEvent)
	{
		if (changeEvent.newValue)
		{
			selfHostedPasswordTextField.SetEnabled(value: true);
			return;
		}
		selfHostedPasswordTextField.SetEnabled(value: false);
		selfHostedPasswordTextField.value = "";
	}

	private void OnSelfHostedPasswordChanged(ChangeEvent<string> changeEvent)
	{
		selfHostedPassword = changeEvent.newValue;
		selfHostedPasswordTextField.value = selfHostedPassword;
	}

	private void OnSelfHostedVoipChanged(ChangeEvent<bool> changeEvent)
	{
		selfHostedVoip = changeEvent.newValue;
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
		return "UIServerLauncher";
	}
}
