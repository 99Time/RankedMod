using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class UISettings : UIComponent<UISettings>
{
	private Slider cameraAngleSlider;

	private DropdownField handednessDropdown;

	private Toggle showPuckSilhouetteToggle;

	private Toggle showPuckOutlineToggle;

	private Toggle showPuckElevationToggle;

	private Toggle showPlayerUsernamesToggle;

	private Slider playerUsernamesFadeThresholdSlider;

	private Toggle useNetworkSmoothingToggle;

	private Slider networkSmoothingStrengthSlider;

	private Toggle filterChatProfanityToggle;

	private DropdownField unitsDropdown;

	private Toggle showGameUserInterfaceToggle;

	private Slider userInterfaceScaleSlider;

	private Slider chatOpacitySlider;

	private Slider chatScaleSlider;

	private Slider minimapOpacitySlider;

	private Slider minimapBackgroundOpacitySlider;

	private Slider minimapHorizontalPositionSlider;

	private Slider minimapVerticalPositionSlider;

	private Slider minimapScaleSlider;

	private Slider globalStickSensitivitySlider;

	private Slider horizontalStickSensitivitySlider;

	private Slider verticalStickSensitivitySlider;

	private Slider lookSensitivitySlider;

	private Dictionary<string, KeyBindControl> keyBindControls;

	private Slider globalVolumeSlider;

	private Slider ambientVolumeSlider;

	private Slider gameVolumeSlider;

	private Slider voiceVolumeSlider;

	private Slider uiVolumeSlider;

	private DropdownField windowModeDropdown;

	private DropdownField displayDropdown;

	private DropdownField resolutionDropdown;

	private Toggle vSyncToggle;

	private Slider fpsLimitSlider;

	private Slider fovSlider;

	private DropdownField qualityDropdown;

	private Toggle motionBlurToggle;

	private Button closeButton;

	private Button resetToDefaultButton;

	private void Start()
	{
		base.VisibilityRequiresMouse = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("SettingsContainer");
		cameraAngleSlider = container.Query<VisualElement>("CameraAngleSlider").First().Query<Slider>("Slider");
		cameraAngleSlider.RegisterValueChangedCallback(OnCameraAngleChanged);
		handednessDropdown = container.Query<VisualElement>("HandednessDropdown").First().Query<DropdownField>("Dropdown");
		handednessDropdown.RegisterValueChangedCallback(OnHandednessChanged);
		showPuckSilhouetteToggle = container.Query<VisualElement>("ShowPuckSilhouetteToggle").First().Query<Toggle>("Toggle");
		showPuckSilhouetteToggle.RegisterValueChangedCallback(OnShowPuckSilhouetteChanged);
		showPuckOutlineToggle = container.Query<VisualElement>("ShowPuckOutlineToggle").First().Query<Toggle>("Toggle");
		showPuckOutlineToggle.RegisterValueChangedCallback(OnShowPuckOutlineChanged);
		showPuckElevationToggle = container.Query<VisualElement>("ShowPuckElevationToggle").First().Query<Toggle>("Toggle");
		showPuckElevationToggle.RegisterValueChangedCallback(OnShowPuckEleveationChanged);
		showPlayerUsernamesToggle = container.Query<VisualElement>("ShowPlayerUsernamesToggle").First().Query<Toggle>("Toggle");
		showPlayerUsernamesToggle.RegisterValueChangedCallback(OnShowPlayerUsernamesChanged);
		playerUsernamesFadeThresholdSlider = container.Query<VisualElement>("PlayerUsernamesFadeThresholdSlider").First().Query<Slider>("Slider");
		playerUsernamesFadeThresholdSlider.RegisterValueChangedCallback(OnPlayerUsernamesFadeThresholdChanged);
		useNetworkSmoothingToggle = container.Query<VisualElement>("UseNetworkSmoothingToggle").First().Query<Toggle>("Toggle");
		useNetworkSmoothingToggle.RegisterValueChangedCallback(OnUseNetworkSmoothingChanged);
		networkSmoothingStrengthSlider = container.Query<VisualElement>("NetworkSmoothingStrengthSlider").First().Query<Slider>("Slider");
		networkSmoothingStrengthSlider.RegisterValueChangedCallback(OnNetworkSmoothingStrengthChanged);
		filterChatProfanityToggle = container.Query<VisualElement>("FilterChatProfanityToggle").First().Query<Toggle>("Toggle");
		filterChatProfanityToggle.RegisterValueChangedCallback(OnFilterChatProfanityChanged);
		unitsDropdown = container.Query<VisualElement>("UnitsDropdown").First().Query<DropdownField>("Dropdown");
		unitsDropdown.RegisterValueChangedCallback(OnUnitsChanged);
		showGameUserInterfaceToggle = container.Query<VisualElement>("ShowGameUserInterfaceToggle").First().Query<Toggle>("Toggle");
		showGameUserInterfaceToggle.RegisterValueChangedCallback(OnShowGameUserInterfaceChanged);
		userInterfaceScaleSlider = container.Query<VisualElement>("UserInterfaceScaleSlider").First().Query<Slider>("Slider");
		userInterfaceScaleSlider.Query("unity-text-field").First().RegisterCallback<ChangeEvent<string>>(OnUserInterfaceScaleChanged);
		userInterfaceScaleSlider.Query("unity-drag-container").First().RegisterCallback<MouseUpEvent>(OnUserInterfaceScaleMouseUp);
		chatOpacitySlider = container.Query<VisualElement>("ChatOpacitySlider").First().Query<Slider>("Slider");
		chatOpacitySlider.RegisterValueChangedCallback(OnChatOpacityChanged);
		chatScaleSlider = container.Query<VisualElement>("ChatScaleSlider").First().Query<Slider>("Slider");
		chatScaleSlider.RegisterValueChangedCallback(OnChatScaleChanged);
		minimapOpacitySlider = container.Query<VisualElement>("MinimapOpacitySlider").First().Query<Slider>("Slider");
		minimapOpacitySlider.RegisterValueChangedCallback(OnMinimapOpacityChanged);
		minimapBackgroundOpacitySlider = container.Query<VisualElement>("MinimapBackgroundOpacitySlider").First().Query<Slider>("Slider");
		minimapBackgroundOpacitySlider.RegisterValueChangedCallback(OnMinimapBackgroundOpacityChanged);
		minimapHorizontalPositionSlider = container.Query<VisualElement>("MinimapHorizontalPositionSlider").First().Query<Slider>("Slider");
		minimapHorizontalPositionSlider.RegisterValueChangedCallback(OnMinimapHorizontalPositionChanged);
		minimapVerticalPositionSlider = container.Query<VisualElement>("MinimapVerticalPositionSlider").First().Query<Slider>("Slider");
		minimapVerticalPositionSlider.RegisterValueChangedCallback(OnMinimapVerticalPositionChanged);
		minimapScaleSlider = container.Query<VisualElement>("MinimapScaleSlider").First().Query<Slider>("Slider");
		minimapScaleSlider.RegisterValueChangedCallback(OnMinimapScaleChanged);
		globalStickSensitivitySlider = container.Query<VisualElement>("GlobalStickSensitivitySlider").First().Query<Slider>("Slider");
		globalStickSensitivitySlider.RegisterValueChangedCallback(OnGlobalStickSensitivityChanged);
		horizontalStickSensitivitySlider = container.Query<VisualElement>("HorizontalStickSensitivitySlider").First().Query<Slider>("Slider");
		horizontalStickSensitivitySlider.RegisterValueChangedCallback(OnHorizontalStickSensitivityChanged);
		verticalStickSensitivitySlider = container.Query<VisualElement>("VerticalStickSensitivitySlider").First().Query<Slider>("Slider");
		verticalStickSensitivitySlider.RegisterValueChangedCallback(OnVerticalStickSensitivityChanged);
		lookSensitivitySlider = container.Query<VisualElement>("LookSensitivitySlider").First().Query<Slider>("Slider");
		lookSensitivitySlider.RegisterValueChangedCallback(OnLookSensitivityChanged);
		keyBindControls = new Dictionary<string, KeyBindControl>
		{
			{
				"Move Forward",
				container.Query<VisualElement>("MoveForwardKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Move Backward",
				container.Query<VisualElement>("MoveBackwardKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Turn Left",
				container.Query<VisualElement>("TurnLeftKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Turn Right",
				container.Query<VisualElement>("TurnRightKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Blade Angle Up",
				container.Query<VisualElement>("BladeAngleUpKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Blade Angle Down",
				container.Query<VisualElement>("BladeAngleDownKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Slide",
				container.Query<VisualElement>("SlideKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Sprint",
				container.Query<VisualElement>("SprintKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Track",
				container.Query<VisualElement>("TrackKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Look",
				container.Query<VisualElement>("LookKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Jump",
				container.Query<VisualElement>("JumpKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Stop",
				container.Query<VisualElement>("StopKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Twist Left",
				container.Query<VisualElement>("TwistLeftKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Twist Right",
				container.Query<VisualElement>("TwistRightKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Dash Left",
				container.Query<VisualElement>("DashLeftKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Dash Right",
				container.Query<VisualElement>("DashRightKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Extend Left",
				container.Query<VisualElement>("ExtendLeftKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Extend Right",
				container.Query<VisualElement>("ExtendRightKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Lateral Left",
				container.Query<VisualElement>("LateralLeftKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Lateral Right",
				container.Query<VisualElement>("LateralRightKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Talk",
				container.Query<VisualElement>("TalkKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"All Chat",
				container.Query<VisualElement>("AllChatKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Team Chat",
				container.Query<VisualElement>("TeamChatKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Position Select",
				container.Query<VisualElement>("PositionSelectKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			},
			{
				"Scoreboard",
				container.Query<VisualElement>("ScoreboardKeyBind").First().Query<KeyBindControl>("KeyBindControl")
			}
		};
		foreach (KeyValuePair<string, KeyBindControl> keyBindControl in keyBindControls)
		{
			keyBindControl.Value.OnClicked = delegate
			{
				OnKeyBindClicked(keyBindControl.Key);
			};
			keyBindControl.Value.OnTypeDropdownValueChanged = delegate(string value)
			{
				OnKeyBindTypeChanged(keyBindControl.Key, value);
			};
		}
		globalVolumeSlider = container.Query<VisualElement>("GlobalVolumeSlider").First().Query<Slider>("Slider");
		globalVolumeSlider.RegisterValueChangedCallback(OnGlobalVolumeChanged);
		ambientVolumeSlider = container.Query<VisualElement>("AmbientVolumeSlider").First().Query<Slider>("Slider");
		ambientVolumeSlider.RegisterValueChangedCallback(OnAmbientVolumeChanged);
		gameVolumeSlider = container.Query<VisualElement>("GameVolumeSlider").First().Query<Slider>("Slider");
		gameVolumeSlider.RegisterValueChangedCallback(OnGameVolumeChanged);
		voiceVolumeSlider = container.Query<VisualElement>("VoiceVolumeSlider").First().Query<Slider>("Slider");
		voiceVolumeSlider.RegisterValueChangedCallback(OnVoiceVolumeChanged);
		uiVolumeSlider = container.Query<VisualElement>("UIVolumeSlider").First().Query<Slider>("Slider");
		uiVolumeSlider.RegisterValueChangedCallback(OnUIVolumeChanged);
		windowModeDropdown = container.Query<VisualElement>("WindowModeDropdown").First().Query<DropdownField>("Dropdown");
		windowModeDropdown.RegisterValueChangedCallback(OnWindowModeChanged);
		displayDropdown = container.Query<VisualElement>("DisplayDropdown").First().Query<DropdownField>("Dropdown");
		displayDropdown.RegisterValueChangedCallback(OnDisplayChanged);
		resolutionDropdown = container.Query<VisualElement>("ResolutionDropdown").First().Query<DropdownField>("Dropdown");
		resolutionDropdown.RegisterValueChangedCallback(OnResolutionChanged);
		vSyncToggle = container.Query<VisualElement>("VSyncToggle").First().Query<Toggle>("Toggle");
		vSyncToggle.RegisterValueChangedCallback(OnVSyncChanged);
		fpsLimitSlider = container.Query<VisualElement>("FPSLimitSlider").First().Query<Slider>("Slider");
		fpsLimitSlider.RegisterValueChangedCallback(OnFpsLimitChanged);
		fovSlider = container.Query<VisualElement>("FOVSlider").First().Query<Slider>("Slider");
		fovSlider.RegisterValueChangedCallback(OnFovChanged);
		qualityDropdown = container.Query<VisualElement>("QualityDropdown").First().Query<DropdownField>("Dropdown");
		qualityDropdown.RegisterValueChangedCallback(OnQualityChanged);
		motionBlurToggle = container.Query<VisualElement>("MotionBlurToggle").First().Query<Toggle>("Toggle");
		motionBlurToggle.RegisterValueChangedCallback(OnMotionBlurChanged);
		closeButton = container.Query<Button>("CloseButton");
		closeButton.clicked += OnClickClose;
		resetToDefaultButton = container.Query<Button>("ResetToDefaultButton");
		resetToDefaultButton.clicked += OnClickResetToDefault;
	}

	public void ApplySettingsValues()
	{
		if (!Application.isBatchMode)
		{
			cameraAngleSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.CameraAngle;
			handednessDropdown.value = MonoBehaviourSingleton<SettingsManager>.Instance.Handedness;
			showPuckSilhouetteToggle.value = MonoBehaviourSingleton<SettingsManager>.Instance.ShowPuckSilhouette > 0;
			showPuckOutlineToggle.value = MonoBehaviourSingleton<SettingsManager>.Instance.ShowPuckOutline > 0;
			showPuckElevationToggle.value = MonoBehaviourSingleton<SettingsManager>.Instance.ShowPuckElevation > 0;
			showPlayerUsernamesToggle.value = MonoBehaviourSingleton<SettingsManager>.Instance.ShowPlayerUsernames > 0;
			playerUsernamesFadeThresholdSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.PlayerUsernamesFadeThreshold;
			useNetworkSmoothingToggle.value = MonoBehaviourSingleton<SettingsManager>.Instance.UseNetworkSmoothing > 0;
			networkSmoothingStrengthSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.NetworkSmoothingStrength;
			filterChatProfanityToggle.value = MonoBehaviourSingleton<SettingsManager>.Instance.FilterChatProfanity > 0;
			unitsDropdown.value = MonoBehaviourSingleton<SettingsManager>.Instance.Units;
			showGameUserInterfaceToggle.value = MonoBehaviourSingleton<SettingsManager>.Instance.ShowGameUserInterface > 0;
			userInterfaceScaleSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.UserInterfaceScale;
			chatOpacitySlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.ChatOpacity;
			chatScaleSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.ChatScale;
			minimapOpacitySlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.MinimapOpacity;
			minimapBackgroundOpacitySlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.MinimapBackgroundOpacity;
			minimapHorizontalPositionSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.MinimapHorizontalPosition;
			minimapVerticalPositionSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.MinimapVerticalPosition;
			minimapScaleSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.MinimapScale;
			globalStickSensitivitySlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.GlobalStickSensitivity;
			horizontalStickSensitivitySlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.HorizontalStickSensitivity;
			verticalStickSensitivitySlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.VerticalStickSensitivity;
			lookSensitivitySlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.LookSensitivity;
			UpdateKeyBinds(MonoBehaviourSingleton<InputManager>.Instance.KeyBinds);
			globalVolumeSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.GlobalVolume;
			ambientVolumeSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.AmbientVolume;
			gameVolumeSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.GameVolume;
			voiceVolumeSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.VoiceVolume;
			uiVolumeSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.UIVolume;
			windowModeDropdown.value = MonoBehaviourSingleton<SettingsManager>.Instance.WindowMode;
			vSyncToggle.value = MonoBehaviourSingleton<SettingsManager>.Instance.VSync > 0;
			fpsLimitSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.FpsLimit;
			fovSlider.value = MonoBehaviourSingleton<SettingsManager>.Instance.Fov;
			qualityDropdown.value = MonoBehaviourSingleton<SettingsManager>.Instance.Quality;
			motionBlurToggle.value = MonoBehaviourSingleton<SettingsManager>.Instance.MotionBlur > 0;
		}
	}

	private void OnClickClose()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsClickClose");
	}

	private void OnClickResetToDefault()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsClickResetToDefault");
	}

	public void UpdateDisplayDropdown(string value)
	{
		if (!Application.isBatchMode)
		{
			displayDropdown.value = value;
			List<DisplayInfo> list = new List<DisplayInfo>();
			Screen.GetDisplayLayout(list);
			List<string> list2 = new List<string>();
			for (int i = 0; i < list.Count; i++)
			{
				list2.Add(list[i].name);
			}
			displayDropdown.choices = list2;
		}
	}

	public void UpdateResolutionsDropdown(string value)
	{
		if (!Application.isBatchMode)
		{
			resolutionDropdown.value = value;
			List<string> list = new List<string>();
			for (int i = 0; i < Screen.resolutions.Length; i++)
			{
				list.Add(Utils.GetResolutionStringFromIndex(i));
			}
			resolutionDropdown.choices = list;
		}
	}

	public void UpdateKeyBinds(Dictionary<string, KeyBind> keyBinds)
	{
		if (Application.isBatchMode)
		{
			return;
		}
		foreach (KeyValuePair<string, KeyBind> keyBind in keyBinds)
		{
			if (!keyBindControls.ContainsKey(keyBind.Key))
			{
				continue;
			}
			KeyBindControl keyBindControl = keyBindControls[keyBind.Key];
			InputAction inputAction = MonoBehaviourSingleton<InputManager>.Instance.RebindableInputActions[keyBind.Key];
			if (inputAction.bindings[0].isComposite)
			{
				string text = null;
				if (!string.IsNullOrEmpty(inputAction.bindings[1].effectivePath))
				{
					text = text + inputAction.GetBindingDisplayString(1, InputBinding.DisplayStringOptions.DontIncludeInteractions).ToUpper() + "+";
				}
				text += inputAction.GetBindingDisplayString(2, InputBinding.DisplayStringOptions.DontIncludeInteractions).ToUpper();
				keyBindControl.PathLabel = text;
			}
			else
			{
				keyBindControl.PathLabel = inputAction.GetBindingDisplayString(0, InputBinding.DisplayStringOptions.DontIncludeInteractions).ToUpper();
			}
			keyBindControl.TypeDropdownValue = Utils.GetHumanizedInteractionFromInteraction(keyBind.Value.Interactions, keyBindControl.IsPressable, keyBindControl.IsHoldable);
		}
	}

	private void OnCameraAngleChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsCameraAngleChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnHandednessChanged(ChangeEvent<string> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsHandednessChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnShowPuckSilhouetteChanged(ChangeEvent<bool> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsShowPuckSilhouetteChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnShowPuckOutlineChanged(ChangeEvent<bool> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsShowPuckOutlineChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnShowPuckEleveationChanged(ChangeEvent<bool> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsShowPuckElevationChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnShowPlayerUsernamesChanged(ChangeEvent<bool> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsShowPlayerUsernamesChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnPlayerUsernamesFadeThresholdChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsPlayerUsernamesFadeThresholdChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnUseNetworkSmoothingChanged(ChangeEvent<bool> changeEvent)
	{
		useNetworkSmoothingToggle.value = changeEvent.newValue;
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsUseNetworkSmoothingChanged", new Dictionary<string, object> { { "value", useNetworkSmoothingToggle.value } });
	}

	private void OnNetworkSmoothingStrengthChanged(ChangeEvent<float> changeEvent)
	{
		networkSmoothingStrengthSlider.value = Mathf.RoundToInt(changeEvent.newValue);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsNetworkSmoothingStrengthChanged", new Dictionary<string, object> { { "value", networkSmoothingStrengthSlider.value } });
	}

	private void OnFilterChatProfanityChanged(ChangeEvent<bool> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsFilterChatProfanityChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnUnitsChanged(ChangeEvent<string> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsUnitsChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnShowGameUserInterfaceChanged(ChangeEvent<bool> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsShowGameUserInterfaceChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnUserInterfaceScaleChanged(ChangeEvent<string> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsUserInterfaceScaleChanged", new Dictionary<string, object> { { "value", userInterfaceScaleSlider.value } });
	}

	private void OnUserInterfaceScaleMouseUp(MouseUpEvent changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsUserInterfaceScaleChanged", new Dictionary<string, object> { { "value", userInterfaceScaleSlider.value } });
	}

	private void OnChatOpacityChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsChatOpacityChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnChatScaleChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsChatScaleChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnMinimapOpacityChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsMinimapOpacityChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnMinimapBackgroundOpacityChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsMinimapBackgroundOpacityChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnMinimapHorizontalPositionChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsMinimapHorizontalPositionChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnMinimapVerticalPositionChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsMinimapVerticalPositionChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnMinimapScaleChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsMinimapScaleChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnGlobalStickSensitivityChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsGlobalStickSensitivityChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnHorizontalStickSensitivityChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsHorizontalStickSensitivityChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnVerticalStickSensitivityChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsVerticalStickSensitivityChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnKeyBindClicked(string actionName)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsKeyBindClicked", new Dictionary<string, object> { { "actionName", actionName } });
	}

	private void OnKeyBindTypeChanged(string actionName, string value)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsKeyBindTypeChanged", new Dictionary<string, object>
		{
			{ "actionName", actionName },
			{ "type", value }
		});
	}

	private void OnLookSensitivityChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsLookSensitivityChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnGlobalVolumeChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsGlobalVolumeChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnAmbientVolumeChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsAmbientVolumeChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnGameVolumeChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsGameVolumeChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnVoiceVolumeChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsVoiceVolumeChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnUIVolumeChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsUIVolumeChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnWindowModeChanged(ChangeEvent<string> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsWindowModeChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnDisplayChanged(ChangeEvent<string> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsDisplayChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnResolutionChanged(ChangeEvent<string> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsResolutionChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnVSyncChanged(ChangeEvent<bool> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsVSyncChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnFpsLimitChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsFpsLimitChanged", new Dictionary<string, object> { 
		{
			"value",
			(int)changeEvent.newValue
		} });
	}

	private void OnFovChanged(ChangeEvent<float> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsFovChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnQualityChanged(ChangeEvent<string> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsQualityChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
	}

	private void OnMotionBlurChanged(ChangeEvent<bool> changeEvent)
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsMotionBlurChanged", new Dictionary<string, object> { { "value", changeEvent.newValue } });
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
		return "UISettings";
	}
}
