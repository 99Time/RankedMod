using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SettingsManagerController : MonoBehaviour
{
	private SettingsManager settingsManager;

	private void Awake()
	{
		settingsManager = GetComponent<SettingsManager>();
		settingsManager.LoadSettings();
	}

	private void Start()
	{
		MonoBehaviourSingleton<InputManager>.Instance.DebugAction.performed += OnDebugActionPerformed;
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsCameraAngleChanged", Event_Client_OnSettingsCameraAngleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsHandednessChanged", Event_Client_OnSettingsHandednessChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsShowPuckSilhouetteChanged", Event_Client_OnSettingsShowPuckSilhouetteChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsShowPuckOutlineChanged", Event_Client_OnSettingsShowPuckOutlineChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsShowPuckElevationChanged", Event_Client_OnSettingsShowPuckElevationChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsShowPlayerUsernamesChanged", Event_Client_OnSettingsShowPlayerUsernamesChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsPlayerUsernamesFadeThresholdChanged", Event_Client_OnSettingsPlayerUsernamesFadeThresholdChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsUseNetworkSmoothingChanged", Event_Client_OnSettingsUseNetworkSmoothingChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsNetworkSmoothingStrengthChanged", Event_Client_OnSettingsNetworkSmoothingStrengthChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsFilterChatProfanityChanged", Event_Client_OnSettingsFilterChatProfanityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsUnitsChanged", Event_Client_OnSettingsUnitsChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsShowGameUserInterfaceChanged", Event_Client_OnSettingsShowGameUserInterfaceChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsUserInterfaceScaleChanged", Event_Client_OnSettingsUserInterfaceScaleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsChatOpacityChanged", Event_Client_OnSettingsChatOpacityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsChatScaleChanged", Event_Client_OnSettingsChatScaleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsMinimapOpacityChanged", Event_Client_OnSettingsMinimapOpacityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsMinimapBackgroundOpacityChanged", Event_Client_OnSettingsMinimapBackgroundOpacityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsMinimapHorizontalPositionChanged", Event_Client_OnSettingsMinimapHorizontalPositionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsMinimapVerticalPositionChanged", Event_Client_OnSettingsMinimapVerticalPositionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsMinimapScaleChanged", Event_Client_OnSettingsMinimapScaleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsGlobalStickSensitivityChanged", Event_Client_OnSettingsGlobalStickSensitivityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsHorizontalStickSensitivityChanged", Event_Client_OnSettingsHorizontalStickSensitivityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsVerticalStickSensitivityChanged", Event_Client_OnSettingsVerticalStickSensitivityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsLookSensitivityChanged", Event_Client_OnSettingsLookSensitivityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsGlobalVolumeChanged", Event_Client_OnSettingsGlobalVolumeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsAmbientVolumeChanged", Event_Client_OnSettingsAmbientVolumeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsGameVolumeChanged", Event_Client_OnSettingsGameVolumeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsVoiceVolumeChanged", Event_Client_OnSettingsVoiceVolumeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsUIVolumeChanged", Event_Client_OnSettingsUIVolumeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsWindowModeChanged", Event_Client_OnSettingsWindowModeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsDisplayChanged", Event_Client_OnSettingsDisplayChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsResolutionChanged", Event_Client_OnSettingsResolutionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsVSyncChanged", Event_Client_OnSettingsVSyncChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsFpsLimitChanged", Event_Client_OnSettingsFpsLimitChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsFovChanged", Event_Client_OnSettingsFovChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsQualityChanged", Event_Client_OnSettingsQualityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsMotionBlurChanged", Event_Client_OnSettingsMotionBlurChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceFlagChanged", Event_Client_OnAppearanceFlagChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceVisorChanged", Event_Client_OnAppearanceVisorChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceMustacheChanged", Event_Client_OnAppearanceMustacheChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceBeardChanged", Event_Client_OnAppearanceBeardChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceJerseyChanged", Event_Client_OnAppearanceJerseyChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceStickSkinChanged", Event_Client_OnAppearanceStickSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceStickShaftTapeSkinChanged", Event_Client_OnAppearanceStickShaftTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceStickBladeTapeSkinChanged", Event_Client_OnAppearanceStickBladeTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnBaseCameraEnabled", Event_Client_OnBaseCameraEnabled);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnAppearanceClickClose", Event_Client_OnAppearanceClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnOwnedItemIdsUpdated", Event_Client_OnOwnedItemIdsUpdated);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPopupClickOk", Event_Client_OnPopupClickOk);
		StartCoroutine(LateStart());
	}

	private IEnumerator LateStart()
	{
		yield return new WaitForEndOfFrame();
		settingsManager.ApplySettings();
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<InputManager>.Instance.DebugAction.performed -= OnDebugActionPerformed;
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsCameraAngleChanged", Event_Client_OnSettingsCameraAngleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsHandednessChanged", Event_Client_OnSettingsHandednessChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsShowPuckSilhouetteChanged", Event_Client_OnSettingsShowPuckSilhouetteChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsShowPuckOutlineChanged", Event_Client_OnSettingsShowPuckOutlineChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsShowPuckElevationChanged", Event_Client_OnSettingsShowPuckElevationChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsShowPlayerUsernamesChanged", Event_Client_OnSettingsShowPlayerUsernamesChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsPlayerUsernamesFadeThresholdChanged", Event_Client_OnSettingsPlayerUsernamesFadeThresholdChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsUseNetworkSmoothingChanged", Event_Client_OnSettingsUseNetworkSmoothingChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsNetworkSmoothingStrengthChanged", Event_Client_OnSettingsNetworkSmoothingStrengthChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsFilterChatProfanityChanged", Event_Client_OnSettingsFilterChatProfanityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsUnitsChanged", Event_Client_OnSettingsUnitsChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsShowGameUserInterfaceChanged", Event_Client_OnSettingsShowGameUserInterfaceChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsUserInterfaceScaleChanged", Event_Client_OnSettingsUserInterfaceScaleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsChatOpacityChanged", Event_Client_OnSettingsChatOpacityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsChatScaleChanged", Event_Client_OnSettingsChatScaleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsMinimapOpacityChanged", Event_Client_OnSettingsMinimapOpacityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsMinimapBackgroundOpacityChanged", Event_Client_OnSettingsMinimapBackgroundOpacityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsMinimapHorizontalPositionChanged", Event_Client_OnSettingsMinimapHorizontalPositionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsMinimapVerticalPositionChanged", Event_Client_OnSettingsMinimapVerticalPositionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsMinimapScaleChanged", Event_Client_OnSettingsMinimapScaleChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsGlobalStickSensitivityChanged", Event_Client_OnSettingsGlobalStickSensitivityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsHorizontalStickSensitivityChanged", Event_Client_OnSettingsHorizontalStickSensitivityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsVerticalStickSensitivityChanged", Event_Client_OnSettingsVerticalStickSensitivityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsLookSensitivityChanged", Event_Client_OnSettingsLookSensitivityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsGlobalVolumeChanged", Event_Client_OnSettingsGlobalVolumeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsAmbientVolumeChanged", Event_Client_OnSettingsAmbientVolumeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsGameVolumeChanged", Event_Client_OnSettingsGameVolumeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsVoiceVolumeChanged", Event_Client_OnSettingsVoiceVolumeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsUIVolumeChanged", Event_Client_OnSettingsUIVolumeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsWindowModeChanged", Event_Client_OnSettingsWindowModeChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsDisplayChanged", Event_Client_OnSettingsDisplayChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsResolutionChanged", Event_Client_OnSettingsResolutionChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsVSyncChanged", Event_Client_OnSettingsVSyncChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsFpsLimitChanged", Event_Client_OnSettingsFpsLimitChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsFovChanged", Event_Client_OnSettingsFovChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsQualityChanged", Event_Client_OnSettingsQualityChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsMotionBlurChanged", Event_Client_OnSettingsMotionBlurChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceFlagChanged", Event_Client_OnAppearanceFlagChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceVisorChanged", Event_Client_OnAppearanceVisorChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceMustacheChanged", Event_Client_OnAppearanceMustacheChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceBeardChanged", Event_Client_OnAppearanceBeardChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceJerseyChanged", Event_Client_OnAppearanceJerseyChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceStickSkinChanged", Event_Client_OnAppearanceStickSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceStickShaftTapeSkinChanged", Event_Client_OnAppearanceStickShaftTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceStickBladeTapeSkinChanged", Event_Client_OnAppearanceStickBladeTapeSkinChanged);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnBaseCameraEnabled", Event_Client_OnBaseCameraEnabled);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnAppearanceClickClose", Event_Client_OnAppearanceClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnOwnedItemIdsUpdated", Event_Client_OnOwnedItemIdsUpdated);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPopupClickOk", Event_Client_OnPopupClickOk);
	}

	private void OnApplicationQuit()
	{
		settingsManager.SaveSettings();
	}

	private void OnDebugActionPerformed(InputAction.CallbackContext context)
	{
		settingsManager.UpdateDebug(settingsManager.Debug == 0);
	}

	private void Event_Client_OnSettingsCameraAngleChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateCameraAngle(value);
	}

	private void Event_Client_OnSettingsHandednessChanged(Dictionary<string, object> message)
	{
		string value = (string)message["value"];
		settingsManager.UpdateHandedness(value);
	}

	private void Event_Client_OnSettingsShowPuckSilhouetteChanged(Dictionary<string, object> message)
	{
		bool value = (bool)message["value"];
		settingsManager.UpdateShowPuckSilhouette(value);
	}

	private void Event_Client_OnSettingsShowPuckOutlineChanged(Dictionary<string, object> message)
	{
		bool value = (bool)message["value"];
		settingsManager.UpdateShowPuckOutline(value);
	}

	private void Event_Client_OnSettingsShowPuckElevationChanged(Dictionary<string, object> message)
	{
		bool value = (bool)message["value"];
		settingsManager.UpdateShowPuckElevation(value);
	}

	private void Event_Client_OnSettingsShowPlayerUsernamesChanged(Dictionary<string, object> message)
	{
		bool value = (bool)message["value"];
		settingsManager.UpdateShowPlayerUsernames(value);
	}

	private void Event_Client_OnSettingsPlayerUsernamesFadeThresholdChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdatePlayerUsernamesFadeThreshold(value);
	}

	private void Event_Client_OnSettingsUseNetworkSmoothingChanged(Dictionary<string, object> message)
	{
		bool value = (bool)message["value"];
		settingsManager.UpdateUseNetworkSmoothing(value);
	}

	private void Event_Client_OnSettingsNetworkSmoothingStrengthChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateNetworkSmoothingStrength(value);
	}

	private void Event_Client_OnSettingsFilterChatProfanityChanged(Dictionary<string, object> message)
	{
		bool value = (bool)message["value"];
		settingsManager.UpdateFilterChatProfanity(value);
	}

	private void Event_Client_OnSettingsUnitsChanged(Dictionary<string, object> message)
	{
		string value = (string)message["value"];
		settingsManager.UpdateUnits(value);
	}

	private void Event_Client_OnSettingsShowGameUserInterfaceChanged(Dictionary<string, object> message)
	{
		bool value = (bool)message["value"];
		settingsManager.UpdateShowGameUserInterface(value);
	}

	private void Event_Client_OnSettingsUserInterfaceScaleChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateUserInterfaceScale(value);
	}

	private void Event_Client_OnSettingsChatOpacityChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateChatOpacity(value);
	}

	private void Event_Client_OnSettingsChatScaleChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateChatScale(value);
	}

	private void Event_Client_OnSettingsMinimapOpacityChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateMinimapOpacity(value);
	}

	private void Event_Client_OnSettingsMinimapBackgroundOpacityChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateMinimapBackgroundOpacity(value);
	}

	private void Event_Client_OnSettingsMinimapHorizontalPositionChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateMinimapHorizontalPosition(value);
	}

	private void Event_Client_OnSettingsMinimapVerticalPositionChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateMinimapVerticalPosition(value);
	}

	private void Event_Client_OnSettingsMinimapScaleChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateMinimapScale(value);
	}

	private void Event_Client_OnSettingsGlobalStickSensitivityChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateGlobalStickSensitivity(value);
	}

	private void Event_Client_OnSettingsHorizontalStickSensitivityChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateHorizontalStickSensitivity(value);
	}

	private void Event_Client_OnSettingsVerticalStickSensitivityChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateVerticalStickSensitivity(value);
	}

	private void Event_Client_OnSettingsLookSensitivityChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateLookSensitivity(value);
	}

	private void Event_Client_OnSettingsGlobalVolumeChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateGlobalVolume(value);
	}

	private void Event_Client_OnSettingsAmbientVolumeChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateAmbientVolume(value);
	}

	private void Event_Client_OnSettingsGameVolumeChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateGameVolume(value);
	}

	private void Event_Client_OnSettingsVoiceVolumeChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateVoiceVolume(value);
	}

	private void Event_Client_OnSettingsUIVolumeChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateUIVolume(value);
	}

	private void Event_Client_OnSettingsWindowModeChanged(Dictionary<string, object> message)
	{
		string value = (string)message["value"];
		settingsManager.UpdateWindowMode(value);
	}

	private void Event_Client_OnSettingsDisplayChanged(Dictionary<string, object> message)
	{
		string text = (string)message["value"];
		int i;
		for (i = 0; i < Display.displays.Length && !(text == Utils.GetDisplayStringFromIndex(i)); i++)
		{
		}
		settingsManager.UpdateDisplayIndex((i != Display.displays.Length) ? i : 0);
	}

	private void Event_Client_OnSettingsResolutionChanged(Dictionary<string, object> message)
	{
		string text = (string)message["value"];
		int i;
		for (i = 0; i < Screen.resolutions.Length && !(text == Utils.GetResolutionStringFromIndex(i)); i++)
		{
		}
		settingsManager.UpdateResolutionIndex((i != Screen.resolutions.Length) ? i : 0);
	}

	private void Event_Client_OnSettingsVSyncChanged(Dictionary<string, object> message)
	{
		bool value = (bool)message["value"];
		settingsManager.UpdateVSync(value);
	}

	private void Event_Client_OnSettingsFpsLimitChanged(Dictionary<string, object> message)
	{
		int value = (int)message["value"];
		settingsManager.UpdateFpsLimit(value);
	}

	private void Event_Client_OnSettingsFovChanged(Dictionary<string, object> message)
	{
		float value = (float)message["value"];
		settingsManager.UpdateFov(value);
	}

	private void Event_Client_OnSettingsQualityChanged(Dictionary<string, object> message)
	{
		string value = (string)message["value"];
		settingsManager.UpdateQuality(value);
	}

	private void Event_Client_OnSettingsMotionBlurChanged(Dictionary<string, object> message)
	{
		bool value = (bool)message["value"];
		settingsManager.UpdateMotionBlur(value);
	}

	private void Event_Client_OnAppearanceFlagChanged(Dictionary<string, object> message)
	{
		string value = (string)message["value"];
		if (!(bool)message["isPreview"])
		{
			settingsManager.UpdateCountry(value);
		}
	}

	private void Event_Client_OnAppearanceVisorChanged(Dictionary<string, object> message)
	{
		PlayerTeam team = (PlayerTeam)message["team"];
		PlayerRole role = (PlayerRole)message["role"];
		string value = (string)message["value"];
		if (!(bool)message["isPreview"])
		{
			settingsManager.UpdateVisorSkin(team, role, value);
		}
	}

	private void Event_Client_OnAppearanceMustacheChanged(Dictionary<string, object> message)
	{
		string value = (string)message["value"];
		if (!(bool)message["isPreview"])
		{
			settingsManager.UpdateMustache(value);
		}
	}

	private void Event_Client_OnAppearanceBeardChanged(Dictionary<string, object> message)
	{
		string value = (string)message["value"];
		if (!(bool)message["isPreview"])
		{
			settingsManager.UpdateBeard(value);
		}
	}

	private void Event_Client_OnAppearanceJerseyChanged(Dictionary<string, object> message)
	{
		PlayerTeam team = (PlayerTeam)message["team"];
		PlayerRole role = (PlayerRole)message["role"];
		string value = (string)message["value"];
		if (!(bool)message["isPreview"])
		{
			settingsManager.UpdateJerseySkin(team, role, value);
		}
	}

	private void Event_Client_OnAppearanceStickSkinChanged(Dictionary<string, object> message)
	{
		PlayerTeam team = (PlayerTeam)message["team"];
		PlayerRole role = (PlayerRole)message["role"];
		string value = (string)message["value"];
		if (!(bool)message["isPreview"])
		{
			settingsManager.UpdateStickSkin(team, role, value);
		}
	}

	private void Event_Client_OnAppearanceStickShaftTapeSkinChanged(Dictionary<string, object> message)
	{
		PlayerTeam team = (PlayerTeam)message["team"];
		PlayerRole role = (PlayerRole)message["role"];
		string value = (string)message["value"];
		if (!(bool)message["isPreview"])
		{
			settingsManager.UpdateStickShaftSkin(team, role, value);
		}
	}

	private void Event_Client_OnAppearanceStickBladeTapeSkinChanged(Dictionary<string, object> message)
	{
		PlayerTeam team = (PlayerTeam)message["team"];
		PlayerRole role = (PlayerRole)message["role"];
		string value = (string)message["value"];
		if (!(bool)message["isPreview"])
		{
			settingsManager.UpdateStickBladeSkin(team, role, value);
		}
	}

	private void Event_Client_OnBaseCameraEnabled(Dictionary<string, object> message)
	{
		((BaseCamera)message["baseCamera"]).SetFieldOfView(settingsManager.Fov);
	}

	private void Event_Client_OnAppearanceClickClose(Dictionary<string, object> message)
	{
		settingsManager.ApplySettings();
	}

	private bool IsItemOwned(string item, List<string> ownedItems, List<string> purchaseableItems)
	{
		if (ownedItems.Contains("all"))
		{
			return true;
		}
		if (!purchaseableItems.Contains(item))
		{
			return true;
		}
		return ownedItems.Contains(item);
	}

	private void ValidateStickSkinOwnership(List<string> ownedItems, List<string> purchaseableItems)
	{
		if (!IsItemOwned(settingsManager.StickAttackerBlueSkin, ownedItems, purchaseableItems))
		{
			settingsManager.UpdateStickSkin(PlayerTeam.Blue, PlayerRole.Attacker, "beta_attacker");
		}
		if (!IsItemOwned(settingsManager.StickAttackerRedSkin, ownedItems, purchaseableItems))
		{
			settingsManager.UpdateStickSkin(PlayerTeam.Red, PlayerRole.Attacker, "beta_attacker");
		}
		if (!IsItemOwned(settingsManager.StickGoalieBlueSkin, ownedItems, purchaseableItems))
		{
			settingsManager.UpdateStickSkin(PlayerTeam.Blue, PlayerRole.Goalie, "beta_goalie");
		}
		if (!IsItemOwned(settingsManager.StickGoalieRedSkin, ownedItems, purchaseableItems))
		{
			settingsManager.UpdateStickSkin(PlayerTeam.Red, PlayerRole.Goalie, "beta_goalie");
		}
	}

	private void ValidateStickTapeSkinOwnership(List<string> ownedItems, List<string> purchaseableItems)
	{
		if (!IsItemOwned(settingsManager.StickShaftAttackerBlueTapeSkin, ownedItems, purchaseableItems))
		{
			settingsManager.UpdateStickShaftSkin(PlayerTeam.Blue, PlayerRole.Attacker, "gray_cloth");
		}
		if (!IsItemOwned(settingsManager.StickShaftAttackerRedTapeSkin, ownedItems, purchaseableItems))
		{
			settingsManager.UpdateStickShaftSkin(PlayerTeam.Red, PlayerRole.Attacker, "gray_cloth");
		}
		if (!IsItemOwned(settingsManager.StickShaftGoalieBlueTapeSkin, ownedItems, purchaseableItems))
		{
			settingsManager.UpdateStickShaftSkin(PlayerTeam.Blue, PlayerRole.Goalie, "gray_cloth");
		}
		if (!IsItemOwned(settingsManager.StickShaftGoalieRedTapeSkin, ownedItems, purchaseableItems))
		{
			settingsManager.UpdateStickShaftSkin(PlayerTeam.Red, PlayerRole.Goalie, "gray_cloth");
		}
		if (!IsItemOwned(settingsManager.StickBladeAttackerBlueTapeSkin, ownedItems, purchaseableItems))
		{
			settingsManager.UpdateStickBladeSkin(PlayerTeam.Blue, PlayerRole.Attacker, "gray_cloth");
		}
		if (!IsItemOwned(settingsManager.StickBladeAttackerRedTapeSkin, ownedItems, purchaseableItems))
		{
			settingsManager.UpdateStickBladeSkin(PlayerTeam.Red, PlayerRole.Attacker, "gray_cloth");
		}
		if (!IsItemOwned(settingsManager.StickBladeGoalieBlueTapeSkin, ownedItems, purchaseableItems))
		{
			settingsManager.UpdateStickBladeSkin(PlayerTeam.Blue, PlayerRole.Goalie, "gray_cloth");
		}
		if (!IsItemOwned(settingsManager.StickBladeGoalieRedTapeSkin, ownedItems, purchaseableItems))
		{
			settingsManager.UpdateStickBladeSkin(PlayerTeam.Red, PlayerRole.Goalie, "gray_cloth");
		}
	}

	private void Event_Client_OnOwnedItemIdsUpdated(Dictionary<string, object> message)
	{
		List<string> ownedItems = (List<string>)message["ownedItems"];
		List<string> purchaseableItems = (List<string>)message["purchaseableItems"];
		ValidateStickSkinOwnership(ownedItems, purchaseableItems);
		ValidateStickTapeSkinOwnership(ownedItems, purchaseableItems);
		settingsManager.ApplySettings();
	}

	private void Event_Client_OnPopupClickOk(Dictionary<string, object> message)
	{
		if (!((string)message["name"] != "settingsResetToDefault"))
		{
			PlayerPrefs.DeleteAll();
			settingsManager.LoadSettings();
			settingsManager.ApplySettings();
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnSettingsResetToDefault");
		}
	}
}
