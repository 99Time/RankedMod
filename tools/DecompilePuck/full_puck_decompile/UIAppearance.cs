using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEngine.UIElements;

public class UIAppearance : UIComponent<UIAppearance>
{
	[Header("Components")]
	public VisualTreeAsset appearanceItemAsset;

	[Header("References")]
	[SerializeField]
	private List<Texture> flagTextures = new List<Texture>();

	[SerializeField]
	private SerializedDictionary<string, AppearanceItem> flagMap = new SerializedDictionary<string, AppearanceItem>();

	[SerializeField]
	private SerializedDictionary<string, AppearanceItem> visorMap = new SerializedDictionary<string, AppearanceItem>();

	[SerializeField]
	private SerializedDictionary<string, AppearanceItem> mustacheMap = new SerializedDictionary<string, AppearanceItem>();

	[SerializeField]
	private SerializedDictionary<string, AppearanceItem> beardMap = new SerializedDictionary<string, AppearanceItem>();

	[SerializeField]
	private SerializedDictionary<string, AppearanceItem> jerseyMap = new SerializedDictionary<string, AppearanceItem>();

	[SerializeField]
	private SerializedDictionary<string, AppearanceItem> stickAttackerSkinMap = new SerializedDictionary<string, AppearanceItem>();

	[SerializeField]
	private SerializedDictionary<string, AppearanceItem> stickGoalieSkinMap = new SerializedDictionary<string, AppearanceItem>();

	[SerializeField]
	private SerializedDictionary<string, AppearanceItem> stickShaftTapeSkinMap = new SerializedDictionary<string, AppearanceItem>();

	[SerializeField]
	private SerializedDictionary<string, AppearanceItem> stickBladeTapeSkinMap = new SerializedDictionary<string, AppearanceItem>();

	private SerializedDictionary<string, AppearanceItem> currentStickSkinMap = new SerializedDictionary<string, AppearanceItem>();

	public bool ApplyForBothTeams;

	private PlayerTeam team = PlayerTeam.Blue;

	private PlayerRole role = PlayerRole.Attacker;

	private TabView tabView;

	private Tab flagTab;

	private Tab visorTab;

	private Tab mustacheTab;

	private Tab beardTab;

	private Tab jerseyTab;

	private Tab stickSkinTab;

	private Tab stickShaftTapeTab;

	private Tab stickBladeTapeTab;

	private Toggle applyForBothTeamsToggle;

	private DropdownField teamDropdown;

	private DropdownField roleDropdown;

	private Button closeButton;

	private RadioButtonGroup flagRadioButtonGroup;

	private RadioButtonGroup visorRadioButtonGroup;

	private RadioButtonGroup mustacheRadioButtonGroup;

	private RadioButtonGroup beardRadioButtonGroup;

	private RadioButtonGroup jerseyRadioButtonGroup;

	private RadioButtonGroup stickSkinRadioButtonGroup;

	private RadioButtonGroup stickShaftTapeRadioButtonGroup;

	private RadioButtonGroup stickBladeTapeRadioButtonGroup;

	private int[] ownedItemIds = new int[0];

	public PlayerTeam Team
	{
		get
		{
			return team;
		}
		set
		{
			if (team != value)
			{
				team = value;
				OnTeamChanged();
			}
		}
	}

	public PlayerRole Role
	{
		get
		{
			return role;
		}
		set
		{
			if (role != value)
			{
				role = value;
				OnRoleChanged();
			}
		}
	}

	private void Start()
	{
		base.VisibilityRequiresMouse = true;
		flagMap.Add("none", new AppearanceItem
		{
			Id = 0,
			Name = "NONE",
			Image = null,
			Purchaseable = false,
			Price = ""
		});
		int num = 0;
		foreach (Texture flagTexture in flagTextures)
		{
			string key = Utils.CountryDictionary.ElementAt(num).Key;
			flagMap.Add(key, new AppearanceItem
			{
				Id = 0,
				Name = Utils.CountryCodeToName(key).ToUpper(),
				Image = flagTexture,
				Purchaseable = false,
				Price = ""
			});
			num++;
		}
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("AppearanceContainer");
		closeButton = container.Query<Button>("CloseButton");
		closeButton.clicked += OnClickClose;
		tabView = container.Query<TabView>("AppearanceTabView");
		tabView.activeTabChanged += OnTabChanged;
		flagTab = tabView.Query<Tab>("FlagTab");
		flagRadioButtonGroup = flagTab.Query<RadioButtonGroup>("AppearanceItemRadioButtonGroup");
		flagRadioButtonGroup.RegisterValueChangedCallback(OnFlagRadioButtonGroupChanged);
		visorTab = tabView.Query<Tab>("VisorTab");
		visorRadioButtonGroup = visorTab.Query<RadioButtonGroup>("AppearanceItemRadioButtonGroup");
		visorRadioButtonGroup.RegisterValueChangedCallback(OnVisorRadioButtonGroupChanged);
		mustacheTab = tabView.Query<Tab>("MustacheTab");
		mustacheRadioButtonGroup = mustacheTab.Query<RadioButtonGroup>("AppearanceItemRadioButtonGroup");
		mustacheRadioButtonGroup.RegisterValueChangedCallback(OnMustacheRadioButtonGroupChanged);
		beardTab = tabView.Query<Tab>("BeardTab");
		beardRadioButtonGroup = beardTab.Query<RadioButtonGroup>("AppearanceItemRadioButtonGroup");
		beardRadioButtonGroup.RegisterValueChangedCallback(OnBeardRadioButtonGroupChanged);
		jerseyTab = tabView.Query<Tab>("JerseyTab");
		jerseyRadioButtonGroup = jerseyTab.Query<RadioButtonGroup>("AppearanceItemRadioButtonGroup");
		jerseyRadioButtonGroup.RegisterValueChangedCallback(OnJerseyRadioButtonGroupChanged);
		stickSkinTab = tabView.Query<Tab>("StickSkinTab");
		stickSkinRadioButtonGroup = stickSkinTab.Query<RadioButtonGroup>("AppearanceItemRadioButtonGroup");
		stickSkinRadioButtonGroup.RegisterValueChangedCallback(OnStickSkinRadioButtonGroupChanged);
		stickShaftTapeTab = tabView.Query<Tab>("StickShaftTapeTab");
		stickShaftTapeRadioButtonGroup = stickShaftTapeTab.Query<RadioButtonGroup>("AppearanceItemRadioButtonGroup");
		stickShaftTapeRadioButtonGroup.RegisterValueChangedCallback(OnStickShaftTapeSkinRadioButtonGroupChanged);
		stickBladeTapeTab = tabView.Query<Tab>("StickBladeTapeTab");
		stickBladeTapeRadioButtonGroup = stickBladeTapeTab.Query<RadioButtonGroup>("AppearanceItemRadioButtonGroup");
		stickBladeTapeRadioButtonGroup.RegisterValueChangedCallback(OnStickBladeTapeSkinRadioButtonGroupChanged);
		applyForBothTeamsToggle = container.Query<VisualElement>("AppearanceApplyForBothTeamsToggle").First().Query<Toggle>("Toggle");
		applyForBothTeamsToggle.RegisterValueChangedCallback(OnApplyForBothTeamsToggleChanged);
		applyForBothTeamsToggle.value = ApplyForBothTeams;
		teamDropdown = container.Query<VisualElement>("AppearanceTeamDropdown").First().Query<DropdownField>("Dropdown");
		teamDropdown.RegisterValueChangedCallback(OnTeamDropdownChanged);
		teamDropdown.value = ((Team == PlayerTeam.Blue) ? "BLUE" : "RED");
		roleDropdown = container.Query<VisualElement>("AppearanceRoleDropdown").First().Query<DropdownField>("Dropdown");
		roleDropdown.RegisterValueChangedCallback(OnRoleDropdownChanged);
		roleDropdown.value = ((Role == PlayerRole.Attacker) ? "SKATER" : "GOALIE");
		Reload();
	}

	public void Reload()
	{
		if (!Application.isBatchMode)
		{
			PopulateAppearanceItemsFromMap(visorRadioButtonGroup, visorMap, ownedItemIds);
			PopulateAppearanceItemsFromMap(flagRadioButtonGroup, flagMap, ownedItemIds);
			PopulateAppearanceItemsFromMap(mustacheRadioButtonGroup, mustacheMap, ownedItemIds);
			PopulateAppearanceItemsFromMap(beardRadioButtonGroup, beardMap, ownedItemIds);
			PopulateAppearanceItemsFromMap(jerseyRadioButtonGroup, jerseyMap, ownedItemIds);
			currentStickSkinMap = ((Role == PlayerRole.Attacker) ? stickAttackerSkinMap : stickGoalieSkinMap);
			PopulateAppearanceItemsFromMap(stickSkinRadioButtonGroup, currentStickSkinMap, ownedItemIds);
			PopulateAppearanceItemsFromMap(stickShaftTapeRadioButtonGroup, stickShaftTapeSkinMap, ownedItemIds);
			PopulateAppearanceItemsFromMap(stickBladeTapeRadioButtonGroup, stickBladeTapeSkinMap, ownedItemIds);
		}
	}

	public void ApplyAppearanceValues()
	{
		if (!Application.isBatchMode)
		{
			flagRadioButtonGroup.value = flagMap.Keys.ToList().IndexOf(MonoBehaviourSingleton<SettingsManager>.Instance.Country);
			visorRadioButtonGroup.value = visorMap.Keys.ToList().IndexOf(MonoBehaviourSingleton<SettingsManager>.Instance.GetVisorSkin(Team, Role));
			mustacheRadioButtonGroup.value = mustacheMap.Keys.ToList().IndexOf(MonoBehaviourSingleton<SettingsManager>.Instance.Mustache);
			beardRadioButtonGroup.value = beardMap.Keys.ToList().IndexOf(MonoBehaviourSingleton<SettingsManager>.Instance.Beard);
			jerseyRadioButtonGroup.value = jerseyMap.Keys.ToList().IndexOf(MonoBehaviourSingleton<SettingsManager>.Instance.GetJerseySkin(Team, Role));
			stickSkinRadioButtonGroup.value = currentStickSkinMap.Keys.ToList().IndexOf(MonoBehaviourSingleton<SettingsManager>.Instance.GetStickSkin(Team, Role));
			stickShaftTapeRadioButtonGroup.value = stickShaftTapeSkinMap.Keys.ToList().IndexOf(MonoBehaviourSingleton<SettingsManager>.Instance.GetStickShaftSkin(Team, Role));
			stickBladeTapeRadioButtonGroup.value = stickBladeTapeSkinMap.Keys.ToList().IndexOf(MonoBehaviourSingleton<SettingsManager>.Instance.GetStickBladeSkin(Team, Role));
		}
	}

	private void PopulateAppearanceItemsFromMap(RadioButtonGroup radioButtonGroup, SerializedDictionary<string, AppearanceItem> map, int[] ownedItemIds)
	{
		radioButtonGroup.contentContainer.Clear();
		int num = 0;
		foreach (KeyValuePair<string, AppearanceItem> item in map)
		{
			AppearanceItem value = item.Value;
			RadioButton radioButton = appearanceItemAsset.Instantiate().Query<RadioButton>("AppearanceItemRadioButton");
			VisualElement visualElement = radioButton.Query<VisualElement>("AppearanceItemImage");
			VisualElement visualElement2 = radioButton.Query<VisualElement>("AppearanceItemPurchaseOverlay");
			radioButton.label = value.Name;
			radioButton.userData = value;
			if (value.Image != null)
			{
				visualElement.style.backgroundImage = (Texture2D)value.Image;
			}
			else if (value.IsTwoTone && value.BlueImage != null && value.RedImage != null)
			{
				if (Team == PlayerTeam.Blue)
				{
					visualElement.style.backgroundImage = (Texture2D)value.BlueImage;
				}
				else
				{
					visualElement.style.backgroundImage = (Texture2D)value.RedImage;
				}
			}
			else
			{
				visualElement.style.backgroundImage = null;
				radioButton.EnableInClassList("no-image", enable: true);
			}
			if (radioButtonGroup.value == num)
			{
				radioButton.value = true;
			}
			if (value.Purchaseable && !ownedItemIds.Contains(value.Id) && !ownedItemIds.Contains(-1))
			{
				visualElement2.style.display = DisplayStyle.Flex;
				Button button = visualElement2.Query<Button>("PurchaseButton");
				button.RegisterCallback<ClickEvent, int>(OnClickPurchase, value.Id);
				button.text = value.Price ?? "";
			}
			else
			{
				visualElement2.style.display = DisplayStyle.None;
			}
			if (value.Hidden && !ownedItemIds.Contains(value.Id) && !ownedItemIds.Contains(-1))
			{
				radioButton.style.display = DisplayStyle.None;
			}
			radioButtonGroup.contentContainer.Add(radioButton);
			num++;
		}
	}

	public void SetOwnedItemIds(int[] itemIds)
	{
		ownedItemIds = itemIds;
		Reload();
		ApplyAppearanceValues();
	}

	public override void Show()
	{
		base.Show();
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceShow");
		OnTabChanged(null, tabView.activeTab);
	}

	public override void Hide(bool ignoreAlwaysVisible = false)
	{
		base.Hide(ignoreAlwaysVisible);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceHide");
	}

	private void OnTeamChanged()
	{
		if (teamDropdown != null)
		{
			teamDropdown.value = ((Team == PlayerTeam.Blue) ? "BLUE" : "RED");
		}
	}

	private void OnRoleChanged()
	{
		if (roleDropdown != null)
		{
			roleDropdown.value = ((Role == PlayerRole.Attacker) ? "SKATER" : "GOALIE");
		}
	}

	private void OnClickClose()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceClickClose");
	}

	private void OnClickPurchase(ClickEvent clickEvent, int itemId)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearancePurchaseItem", new Dictionary<string, object> { { "itemId", itemId } });
	}

	private void OnTeamDropdownChanged(ChangeEvent<string> changeEvent)
	{
		Team = ((changeEvent.newValue == "BLUE") ? PlayerTeam.Blue : PlayerTeam.Red);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceTeamChanged", new Dictionary<string, object> { { "team", Team } });
	}

	private void OnRoleDropdownChanged(ChangeEvent<string> changeEvent)
	{
		Role = ((changeEvent.newValue == "SKATER") ? PlayerRole.Attacker : PlayerRole.Goalie);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceRoleChanged", new Dictionary<string, object> { { "role", Role } });
	}

	private void OnApplyForBothTeamsToggleChanged(ChangeEvent<bool> changeEvent)
	{
		ApplyForBothTeams = changeEvent.newValue;
	}

	private void OnTabChanged(Tab oldTab, Tab newTab)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceTabChanged", new Dictionary<string, object> { { "tabName", newTab.name } });
	}

	private void OnFlagRadioButtonGroupChanged(ChangeEvent<int> changeEvent)
	{
		if (flagMap.Count > changeEvent.newValue && changeEvent.newValue >= 0)
		{
			string key = flagMap.ElementAt(changeEvent.newValue).Key;
			AppearanceItem value = flagMap.ElementAt(changeEvent.newValue).Value;
			bool flag = value.Purchaseable && !ownedItemIds.Contains(value.Id) && !ownedItemIds.Contains(-1);
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceFlagChanged", new Dictionary<string, object>
			{
				{ "isPreview", flag },
				{ "value", key }
			});
		}
	}

	private void OnVisorRadioButtonGroupChanged(ChangeEvent<int> changeEvent)
	{
		if (visorMap.Count > changeEvent.newValue && changeEvent.newValue >= 0)
		{
			string key = visorMap.ElementAt(changeEvent.newValue).Key;
			AppearanceItem value = visorMap.ElementAt(changeEvent.newValue).Value;
			bool flag = value.Purchaseable && !ownedItemIds.Contains(value.Id) && !ownedItemIds.Contains(-1);
			if (ApplyForBothTeams)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceVisorChanged", new Dictionary<string, object>
				{
					{
						"team",
						(Team == PlayerTeam.Blue) ? PlayerTeam.Red : PlayerTeam.Blue
					},
					{ "role", Role },
					{ "isPreview", flag },
					{ "value", key }
				});
			}
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceVisorChanged", new Dictionary<string, object>
			{
				{ "team", Team },
				{ "role", Role },
				{ "isPreview", flag },
				{ "value", key }
			});
		}
	}

	private void OnMustacheRadioButtonGroupChanged(ChangeEvent<int> changeEvent)
	{
		if (mustacheMap.Count > changeEvent.newValue && changeEvent.newValue >= 0)
		{
			string key = mustacheMap.ElementAt(changeEvent.newValue).Key;
			AppearanceItem value = mustacheMap.ElementAt(changeEvent.newValue).Value;
			bool flag = value.Purchaseable && !ownedItemIds.Contains(value.Id) && !ownedItemIds.Contains(-1);
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceMustacheChanged", new Dictionary<string, object>
			{
				{ "isPreview", flag },
				{ "value", key }
			});
		}
	}

	private void OnBeardRadioButtonGroupChanged(ChangeEvent<int> changeEvent)
	{
		if (beardMap.Count > changeEvent.newValue && changeEvent.newValue >= 0)
		{
			string key = beardMap.ElementAt(changeEvent.newValue).Key;
			AppearanceItem value = beardMap.ElementAt(changeEvent.newValue).Value;
			bool flag = value.Purchaseable && !ownedItemIds.Contains(value.Id) && !ownedItemIds.Contains(-1);
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceBeardChanged", new Dictionary<string, object>
			{
				{ "isPreview", flag },
				{ "value", key }
			});
		}
	}

	private void OnJerseyRadioButtonGroupChanged(ChangeEvent<int> changeEvent)
	{
		if (jerseyMap.Count > changeEvent.newValue && changeEvent.newValue >= 0)
		{
			AppearanceItem value = jerseyMap.ElementAt(changeEvent.newValue).Value;
			string key = jerseyMap.ElementAt(changeEvent.newValue).Key;
			bool flag = value.Purchaseable && !ownedItemIds.Contains(value.Id) && !ownedItemIds.Contains(-1);
			if (ApplyForBothTeams)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceJerseyChanged", new Dictionary<string, object>
				{
					{
						"team",
						(Team == PlayerTeam.Blue) ? PlayerTeam.Red : PlayerTeam.Blue
					},
					{ "role", Role },
					{ "isPreview", flag },
					{ "value", key }
				});
			}
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceJerseyChanged", new Dictionary<string, object>
			{
				{ "team", Team },
				{ "role", Role },
				{ "isPreview", flag },
				{ "value", key }
			});
		}
	}

	private void OnStickSkinRadioButtonGroupChanged(ChangeEvent<int> changeEvent)
	{
		if (currentStickSkinMap.Count > changeEvent.newValue && changeEvent.newValue >= 0)
		{
			AppearanceItem value = currentStickSkinMap.ElementAt(changeEvent.newValue).Value;
			string key = currentStickSkinMap.ElementAt(changeEvent.newValue).Key;
			bool flag = value.Purchaseable && !ownedItemIds.Contains(value.Id) && !ownedItemIds.Contains(-1);
			if (ApplyForBothTeams)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceStickSkinChanged", new Dictionary<string, object>
				{
					{
						"team",
						(Team == PlayerTeam.Blue) ? PlayerTeam.Red : PlayerTeam.Blue
					},
					{ "role", Role },
					{ "isPreview", flag },
					{ "value", key }
				});
			}
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceStickSkinChanged", new Dictionary<string, object>
			{
				{ "team", Team },
				{ "role", Role },
				{ "isPreview", flag },
				{ "value", key }
			});
		}
	}

	private void OnStickShaftTapeSkinRadioButtonGroupChanged(ChangeEvent<int> changeEvent)
	{
		if (stickShaftTapeSkinMap.Count > changeEvent.newValue && changeEvent.newValue >= 0)
		{
			string key = stickShaftTapeSkinMap.ElementAt(changeEvent.newValue).Key;
			AppearanceItem value = stickShaftTapeSkinMap.ElementAt(changeEvent.newValue).Value;
			bool flag = value.Purchaseable && !ownedItemIds.Contains(value.Id) && !ownedItemIds.Contains(-1);
			if (ApplyForBothTeams)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceStickShaftTapeSkinChanged", new Dictionary<string, object>
				{
					{
						"team",
						(Team == PlayerTeam.Blue) ? PlayerTeam.Red : PlayerTeam.Blue
					},
					{ "role", Role },
					{ "isPreview", flag },
					{ "value", key }
				});
			}
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceStickShaftTapeSkinChanged", new Dictionary<string, object>
			{
				{ "team", Team },
				{ "role", Role },
				{ "isPreview", flag },
				{ "value", key }
			});
		}
	}

	private void OnStickBladeTapeSkinRadioButtonGroupChanged(ChangeEvent<int> changeEvent)
	{
		if (stickBladeTapeSkinMap.Count > changeEvent.newValue && changeEvent.newValue >= 0)
		{
			string key = stickBladeTapeSkinMap.ElementAt(changeEvent.newValue).Key;
			AppearanceItem value = stickBladeTapeSkinMap.ElementAt(changeEvent.newValue).Value;
			bool flag = value.Purchaseable && !ownedItemIds.Contains(value.Id) && !ownedItemIds.Contains(-1);
			if (ApplyForBothTeams)
			{
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceStickBladeTapeSkinChanged", new Dictionary<string, object>
				{
					{
						"team",
						(Team == PlayerTeam.Blue) ? PlayerTeam.Red : PlayerTeam.Blue
					},
					{ "role", Role },
					{ "isPreview", flag },
					{ "value", key }
				});
			}
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnAppearanceStickBladeTapeSkinChanged", new Dictionary<string, object>
			{
				{ "team", Team },
				{ "role", Role },
				{ "isPreview", flag },
				{ "value", key }
			});
		}
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
		return "UIAppearance";
	}
}
