using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SettingsManager : MonoBehaviourSingleton<SettingsManager>
{
	[Header("References")]
	public AudioMixer audioMixer;

	public int Debug;

	public float CameraAngle;

	public string Handedness;

	public int ShowPuckSilhouette;

	public int ShowPuckOutline;

	public int ShowPuckElevation;

	public int ShowPlayerUsernames;

	public float PlayerUsernamesFadeThreshold;

	public int UseNetworkSmoothing;

	public float NetworkSmoothingStrength;

	public int FilterChatProfanity;

	public string Units;

	public int ShowGameUserInterface;

	public float UserInterfaceScale;

	public float ChatOpacity;

	public float ChatScale;

	public float MinimapOpacity;

	public float MinimapBackgroundOpacity;

	public float MinimapHorizontalPosition;

	public float MinimapVerticalPosition;

	public float MinimapScale;

	public float GlobalStickSensitivity;

	public float HorizontalStickSensitivity;

	public float VerticalStickSensitivity;

	public float LookSensitivity;

	public float GlobalVolume;

	public float AmbientVolume;

	public float GameVolume;

	public float VoiceVolume;

	public float UIVolume;

	public string WindowMode;

	public int DisplayIndex;

	public int ResolutionIndex;

	public int VSync;

	public int FpsLimit;

	public float Fov;

	public string Quality;

	public int MotionBlur;

	public string Country;

	public string VisorAttackerBlueSkin;

	public string VisorAttackerRedSkin;

	public string VisorGoalieBlueSkin;

	public string VisorGoalieRedSkin;

	public string Mustache;

	public string Beard;

	public string JerseyAttackerBlueSkin;

	public string JerseyAttackerRedSkin;

	public string JerseyGoalieBlueSkin;

	public string JerseyGoalieRedSkin;

	public string StickAttackerBlueSkin;

	public string StickAttackerRedSkin;

	public string StickGoalieBlueSkin;

	public string StickGoalieRedSkin;

	public string StickShaftAttackerBlueTapeSkin;

	public string StickShaftAttackerRedTapeSkin;

	public string StickShaftGoalieBlueTapeSkin;

	public string StickShaftGoalieRedTapeSkin;

	public string StickBladeAttackerBlueTapeSkin;

	public string StickBladeAttackerRedTapeSkin;

	public string StickBladeGoalieBlueTapeSkin;

	public string StickBladeGoalieRedTapeSkin;

	public void LoadSettings()
	{
		if (!Application.isBatchMode)
		{
			Debug = PlayerPrefs.GetInt("debug", 0);
			CameraAngle = PlayerPrefs.GetFloat("cameraAngle", 30f);
			Handedness = PlayerPrefs.GetString("handedness", "RIGHT");
			ShowPuckSilhouette = PlayerPrefs.GetInt("showPuckSilhouette", 1);
			ShowPuckOutline = PlayerPrefs.GetInt("showPuckOutline", 0);
			ShowPuckElevation = PlayerPrefs.GetInt("showPuckElevation", 1);
			ShowPlayerUsernames = PlayerPrefs.GetInt("showPlayerUsernames", 0);
			PlayerUsernamesFadeThreshold = PlayerPrefs.GetFloat("playerUsernamesFadeThreshold", 1f);
			UseNetworkSmoothing = PlayerPrefs.GetInt("useNetworkSmoothing", 0);
			NetworkSmoothingStrength = PlayerPrefs.GetFloat("networkSmoothingStrength", 1f);
			FilterChatProfanity = PlayerPrefs.GetInt("filterChatProfanity", 1);
			Units = PlayerPrefs.GetString("units", "METRIC");
			ShowGameUserInterface = PlayerPrefs.GetInt("showGameUserInterface", 1);
			UserInterfaceScale = PlayerPrefs.GetFloat("userInterfaceScale", 1f);
			ChatOpacity = PlayerPrefs.GetFloat("chatOpacity", 1f);
			ChatScale = PlayerPrefs.GetFloat("chatScale", 1f);
			MinimapOpacity = PlayerPrefs.GetFloat("minimapOpacity", 1f);
			MinimapBackgroundOpacity = PlayerPrefs.GetFloat("minimapBackgroundOpacity", 0.25f);
			MinimapHorizontalPosition = PlayerPrefs.GetFloat("minimapHorizontalPosition", 100f);
			MinimapVerticalPosition = PlayerPrefs.GetFloat("minimapVerticalPosition", 100f);
			MinimapScale = PlayerPrefs.GetFloat("minimapScale", 1f);
			GlobalStickSensitivity = PlayerPrefs.GetFloat("globalStickSensitivity", 0.2f);
			HorizontalStickSensitivity = PlayerPrefs.GetFloat("horizontalStickSensitivity", 1f);
			VerticalStickSensitivity = PlayerPrefs.GetFloat("verticalStickSensitivity", 1f);
			LookSensitivity = PlayerPrefs.GetFloat("lookSensitivity", 0.2f);
			GlobalVolume = PlayerPrefs.GetFloat("globalVolume", 0.5f);
			AmbientVolume = PlayerPrefs.GetFloat("ambientVolume", 1f);
			GameVolume = PlayerPrefs.GetFloat("gameVolume", 1f);
			VoiceVolume = PlayerPrefs.GetFloat("voiceVolume", 1f);
			UIVolume = PlayerPrefs.GetFloat("uiVolume", 0.5f);
			WindowMode = PlayerPrefs.GetString("windowMode", "BORDERLESS");
			DisplayIndex = PlayerPrefs.GetInt("displayIndex", -1);
			ResolutionIndex = PlayerPrefs.GetInt("resolutionIndex", -1);
			VSync = PlayerPrefs.GetInt("vSync", 0);
			FpsLimit = PlayerPrefs.GetInt("fpsLimit", 240);
			Fov = PlayerPrefs.GetFloat("fov", 90f);
			Quality = PlayerPrefs.GetString("quality", "HIGH");
			MotionBlur = PlayerPrefs.GetInt("motionBlur", 1);
			Country = PlayerPrefs.GetString("country", "none");
			VisorAttackerBlueSkin = PlayerPrefs.GetString("visorAttackerBlueSkin", "visor_default_attacker");
			VisorAttackerRedSkin = PlayerPrefs.GetString("visorAttackerRedSkin", "visor_default_attacker");
			VisorGoalieBlueSkin = PlayerPrefs.GetString("visorGoalieBlueSkin", "visor_default_attacker");
			VisorGoalieRedSkin = PlayerPrefs.GetString("visorGoalieRedSkin", "visor_default_attacker");
			Mustache = PlayerPrefs.GetString("mustache", "none");
			Beard = PlayerPrefs.GetString("beard", "none");
			JerseyAttackerBlueSkin = PlayerPrefs.GetString("jerseyAttackerBlueSkin", "default");
			JerseyAttackerRedSkin = PlayerPrefs.GetString("jerseyAttackerRedSkin", "default");
			JerseyGoalieBlueSkin = PlayerPrefs.GetString("jerseyGoalieBlueSkin", "default");
			JerseyGoalieRedSkin = PlayerPrefs.GetString("jerseyGoalieRedSkin", "default");
			StickAttackerBlueSkin = PlayerPrefs.GetString("stickAttackerBlueSkin", "classic_attacker");
			StickAttackerRedSkin = PlayerPrefs.GetString("stickAttackerRedSkin", "classic_attacker");
			StickGoalieBlueSkin = PlayerPrefs.GetString("stickGoalieBlueSkin", "classic_goalie");
			StickGoalieRedSkin = PlayerPrefs.GetString("stickGoalieRedSkin", "classic_goalie");
			StickShaftAttackerBlueTapeSkin = PlayerPrefs.GetString("stickShaftAttackerBlueTapeSkin", "gray_cloth");
			StickShaftAttackerRedTapeSkin = PlayerPrefs.GetString("stickShaftAttackerRedTapeSkin", "gray_cloth");
			StickShaftGoalieBlueTapeSkin = PlayerPrefs.GetString("stickShaftGoalieBlueTapeSkin", "gray_cloth");
			StickShaftGoalieRedTapeSkin = PlayerPrefs.GetString("stickShaftGoalieRedTapeSkin", "gray_cloth");
			StickBladeAttackerBlueTapeSkin = PlayerPrefs.GetString("stickBladeAttackerBlueTapeSkin", "gray_cloth");
			StickBladeAttackerRedTapeSkin = PlayerPrefs.GetString("stickBladeAttackerRedTapeSkin", "gray_cloth");
			StickBladeGoalieBlueTapeSkin = PlayerPrefs.GetString("stickBladeGoalieBlueTapeSkin", "gray_cloth");
			StickBladeGoalieRedTapeSkin = PlayerPrefs.GetString("stickBladeGoalieRedTapeSkin", "gray_cloth");
		}
	}

	public void ApplySettings()
	{
		if (!Application.isBatchMode)
		{
			UpdateDebug(Debug > 0);
			UpdateCameraAngle(CameraAngle);
			UpdateHandedness(Handedness);
			UpdateShowPuckSilhouette(ShowPuckSilhouette > 0);
			UpdateShowPuckOutline(ShowPuckOutline > 0);
			UpdateShowPuckElevation(ShowPuckElevation > 0);
			UpdateShowPlayerUsernames(ShowPlayerUsernames > 0);
			UpdatePlayerUsernamesFadeThreshold(PlayerUsernamesFadeThreshold);
			UpdateUseNetworkSmoothing(UseNetworkSmoothing > 0);
			UpdateNetworkSmoothingStrength(NetworkSmoothingStrength);
			UpdateFilterChatProfanity(FilterChatProfanity > 0);
			UpdateUnits(Units);
			UpdateShowGameUserInterface(ShowGameUserInterface > 0);
			UpdateUserInterfaceScale(UserInterfaceScale);
			UpdateChatOpacity(ChatOpacity);
			UpdateChatScale(ChatScale);
			UpdateMinimapOpacity(MinimapOpacity);
			UpdateMinimapBackgroundOpacity(MinimapBackgroundOpacity);
			UpdateMinimapHorizontalPosition(MinimapHorizontalPosition);
			UpdateMinimapVerticalPosition(MinimapVerticalPosition);
			UpdateMinimapScale(MinimapScale);
			UpdateGlobalStickSensitivity(GlobalStickSensitivity);
			UpdateHorizontalStickSensitivity(HorizontalStickSensitivity);
			UpdateVerticalStickSensitivity(VerticalStickSensitivity);
			UpdateLookSensitivity(LookSensitivity);
			UpdateGlobalVolume(GlobalVolume);
			UpdateAmbientVolume(AmbientVolume);
			UpdateGameVolume(GameVolume);
			UpdateVoiceVolume(VoiceVolume);
			UpdateUIVolume(UIVolume);
			UpdateWindowMode(WindowMode);
			UpdateDisplayIndex(DisplayIndex, isInitialLoad: true);
			UpdateVSync(VSync > 0);
			UpdateFpsLimit(FpsLimit);
			UpdateFov(Fov);
			UpdateQuality(Quality);
			UpdateMotionBlur(MotionBlur > 0);
			UpdateCountry(Country);
			UpdateVisorSkin(PlayerTeam.Blue, PlayerRole.Attacker, VisorAttackerBlueSkin);
			UpdateMustache(Mustache);
			UpdateBeard(Beard);
			UpdateJerseySkin(PlayerTeam.Blue, PlayerRole.Attacker, JerseyAttackerBlueSkin);
			UpdateStickSkin(PlayerTeam.Blue, PlayerRole.Attacker, StickAttackerBlueSkin);
			UpdateStickShaftSkin(PlayerTeam.Blue, PlayerRole.Attacker, StickShaftAttackerBlueTapeSkin);
		}
	}

	public void SaveSettings()
	{
		if (!Application.isBatchMode)
		{
			PlayerPrefs.Save();
		}
	}

	public string GetVisorSkin(PlayerTeam team, PlayerRole role)
	{
		switch (role)
		{
		case PlayerRole.Attacker:
			switch (team)
			{
			case PlayerTeam.Blue:
				return VisorAttackerBlueSkin;
			case PlayerTeam.Red:
				return VisorAttackerRedSkin;
			}
			break;
		case PlayerRole.Goalie:
			switch (team)
			{
			case PlayerTeam.Blue:
				return VisorGoalieBlueSkin;
			case PlayerTeam.Red:
				return VisorGoalieRedSkin;
			}
			break;
		}
		return "default";
	}

	public string GetJerseySkin(PlayerTeam team, PlayerRole role)
	{
		switch (role)
		{
		case PlayerRole.Attacker:
			switch (team)
			{
			case PlayerTeam.Blue:
				return JerseyAttackerBlueSkin;
			case PlayerTeam.Red:
				return JerseyAttackerRedSkin;
			}
			break;
		case PlayerRole.Goalie:
			switch (team)
			{
			case PlayerTeam.Blue:
				return JerseyGoalieBlueSkin;
			case PlayerTeam.Red:
				return JerseyGoalieRedSkin;
			}
			break;
		}
		return "default";
	}

	public string GetStickSkin(PlayerTeam team, PlayerRole role)
	{
		switch (role)
		{
		case PlayerRole.Attacker:
			switch (team)
			{
			case PlayerTeam.Blue:
				return StickAttackerBlueSkin;
			case PlayerTeam.Red:
				return StickAttackerRedSkin;
			}
			break;
		case PlayerRole.Goalie:
			switch (team)
			{
			case PlayerTeam.Blue:
				return StickGoalieBlueSkin;
			case PlayerTeam.Red:
				return StickGoalieRedSkin;
			}
			break;
		}
		return "classic";
	}

	public string GetStickShaftSkin(PlayerTeam team, PlayerRole role)
	{
		switch (role)
		{
		case PlayerRole.Attacker:
			switch (team)
			{
			case PlayerTeam.Blue:
				return StickShaftAttackerBlueTapeSkin;
			case PlayerTeam.Red:
				return StickShaftAttackerRedTapeSkin;
			}
			break;
		case PlayerRole.Goalie:
			switch (team)
			{
			case PlayerTeam.Blue:
				return StickShaftGoalieBlueTapeSkin;
			case PlayerTeam.Red:
				return StickShaftGoalieRedTapeSkin;
			}
			break;
		}
		return "gray";
	}

	public string GetStickBladeSkin(PlayerTeam team, PlayerRole role)
	{
		switch (role)
		{
		case PlayerRole.Attacker:
			switch (team)
			{
			case PlayerTeam.Blue:
				return StickBladeAttackerBlueTapeSkin;
			case PlayerTeam.Red:
				return StickBladeAttackerRedTapeSkin;
			}
			break;
		case PlayerRole.Goalie:
			switch (team)
			{
			case PlayerTeam.Blue:
				return StickBladeGoalieBlueTapeSkin;
			case PlayerTeam.Red:
				return StickBladeGoalieRedTapeSkin;
			}
			break;
		}
		return "gray";
	}

	public void UpdateDebug(bool value)
	{
		Debug = (value ? 1 : 0);
		PlayerPrefs.SetInt("debug", Debug);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnDebugChanged", new Dictionary<string, object> { { "value", Debug } });
	}

	public void UpdateCameraAngle(float value)
	{
		CameraAngle = value;
		PlayerPrefs.SetFloat("cameraAngle", CameraAngle);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnCameraAngleChanged", new Dictionary<string, object> { { "value", CameraAngle } });
	}

	public void UpdateHandedness(string value)
	{
		Handedness = value;
		PlayerPrefs.SetString("handedness", Handedness);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnHandednessChanged", new Dictionary<string, object> { { "value", Handedness } });
	}

	public void UpdateShowPuckSilhouette(bool value)
	{
		ShowPuckSilhouette = (value ? 1 : 0);
		PlayerPrefs.SetInt("showPuckSilhouette", ShowPuckSilhouette);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnShowPuckSilhouetteChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateShowPuckOutline(bool value)
	{
		ShowPuckOutline = (value ? 1 : 0);
		PlayerPrefs.SetInt("showPuckOutline", ShowPuckOutline);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnShowPuckOutlineChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateShowPuckElevation(bool value)
	{
		ShowPuckElevation = (value ? 1 : 0);
		PlayerPrefs.SetInt("showPuckElevation", ShowPuckElevation);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnShowPuckElevationChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateShowPlayerUsernames(bool value)
	{
		ShowPlayerUsernames = (value ? 1 : 0);
		PlayerPrefs.SetInt("showPlayerUsernames", ShowPlayerUsernames);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnShowPlayerUsernamesChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdatePlayerUsernamesFadeThreshold(float value)
	{
		PlayerUsernamesFadeThreshold = value;
		PlayerPrefs.SetFloat("playerUsernamesFadeThreshold", PlayerUsernamesFadeThreshold);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerUsernamesFadeThresholdChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateUseNetworkSmoothing(bool value)
	{
		UseNetworkSmoothing = (value ? 1 : 0);
		PlayerPrefs.SetInt("useNetworkSmoothing", UseNetworkSmoothing);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnUseNetworkSmoothingChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateNetworkSmoothingStrength(float value)
	{
		NetworkSmoothingStrength = value;
		PlayerPrefs.SetFloat("networkSmoothingStrength", NetworkSmoothingStrength);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnNetworkSmoothingStrengthChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateFilterChatProfanity(bool value)
	{
		FilterChatProfanity = (value ? 1 : 0);
		PlayerPrefs.SetInt("filterChatProfanity", FilterChatProfanity);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnFilterChatProfanityChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateUnits(string value)
	{
		Units = value;
		PlayerPrefs.SetString("units", Units);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnUnitsChanged", new Dictionary<string, object> { { "value", Units } });
	}

	public void UpdateShowGameUserInterface(bool value)
	{
		ShowGameUserInterface = (value ? 1 : 0);
		PlayerPrefs.SetInt("showGameUserInterface", ShowGameUserInterface);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnShowGameUserInterfaceChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateUserInterfaceScale(float value)
	{
		UserInterfaceScale = value;
		PlayerPrefs.SetFloat("userInterfaceScale", UserInterfaceScale);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnUserInterfaceScaleChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateChatOpacity(float value)
	{
		ChatOpacity = value;
		PlayerPrefs.SetFloat("chatOpacity", ChatOpacity);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnChatOpacityChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateChatScale(float value)
	{
		ChatScale = value;
		PlayerPrefs.SetFloat("chatScale", ChatScale);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnChatScaleChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateMinimapOpacity(float value)
	{
		MinimapOpacity = value;
		PlayerPrefs.SetFloat("minimapOpacity", MinimapOpacity);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMinimapOpacityChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateMinimapBackgroundOpacity(float value)
	{
		MinimapBackgroundOpacity = value;
		PlayerPrefs.SetFloat("minimapBackgroundOpacity", MinimapBackgroundOpacity);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMinimapBackgroundOpacityChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateMinimapHorizontalPosition(float value)
	{
		MinimapHorizontalPosition = value;
		PlayerPrefs.SetFloat("minimapHorizontalPosition", MinimapHorizontalPosition);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMinimapHorizontalPositionChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateMinimapVerticalPosition(float value)
	{
		MinimapVerticalPosition = value;
		PlayerPrefs.SetFloat("minimapVerticalPosition", MinimapVerticalPosition);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMinimapVerticalPositionChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateMinimapScale(float value)
	{
		MinimapScale = value;
		PlayerPrefs.SetFloat("minimapScale", MinimapScale);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMinimapScaleChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateGlobalStickSensitivity(float value)
	{
		GlobalStickSensitivity = value;
		PlayerPrefs.SetFloat("globalStickSensitivity", GlobalStickSensitivity);
	}

	public void UpdateHorizontalStickSensitivity(float value)
	{
		HorizontalStickSensitivity = value;
		PlayerPrefs.SetFloat("horizontalStickSensitivity", HorizontalStickSensitivity);
	}

	public void UpdateVerticalStickSensitivity(float value)
	{
		VerticalStickSensitivity = value;
		PlayerPrefs.SetFloat("verticalStickSensitivity", VerticalStickSensitivity);
	}

	public void UpdateLookSensitivity(float value)
	{
		LookSensitivity = value;
		PlayerPrefs.SetFloat("lookSensitivity", LookSensitivity);
	}

	public void UpdateGlobalVolume(float value)
	{
		GlobalVolume = value;
		PlayerPrefs.SetFloat("globalVolume", GlobalVolume);
		audioMixer.SetFloat("globalVolume", Mathf.Log(GlobalVolume + 0.001f) * 20f);
	}

	public void UpdateAmbientVolume(float value)
	{
		AmbientVolume = value;
		PlayerPrefs.SetFloat("ambientVolume", AmbientVolume);
		audioMixer.SetFloat("ambientVolume", Mathf.Log(AmbientVolume + 0.001f) * 20f);
	}

	public void UpdateGameVolume(float value)
	{
		GameVolume = value;
		PlayerPrefs.SetFloat("gameVolume", GameVolume);
		audioMixer.SetFloat("gameVolume", Mathf.Log(GameVolume + 0.001f) * 20f);
	}

	public void UpdateVoiceVolume(float value)
	{
		VoiceVolume = value;
		PlayerPrefs.SetFloat("voiceVolume", VoiceVolume);
		audioMixer.SetFloat("voiceVolume", Mathf.Log(VoiceVolume + 0.001f) * 20f);
	}

	public void UpdateUIVolume(float value)
	{
		UIVolume = value;
		PlayerPrefs.SetFloat("uiVolume", UIVolume);
		audioMixer.SetFloat("uiVolume", Mathf.Log(UIVolume + 0.001f) * 20f);
	}

	public void UpdateWindowMode(string value)
	{
		WindowMode = value;
		PlayerPrefs.SetString("windowMode", WindowMode);
		switch (WindowMode)
		{
		case "FULLSCREEN":
			Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
			break;
		case "BORDERLESS":
			Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
			break;
		case "WINDOWED":
			Screen.fullScreenMode = FullScreenMode.MaximizedWindow;
			break;
		}
	}

	public async void UpdateDisplayIndex(int value, bool isInitialLoad = false)
	{
		List<DisplayInfo> displayInfos = new List<DisplayInfo>();
		Screen.GetDisplayLayout(displayInfos);
		DisplayIndex = ((value > displayInfos.Count - 1 || value < 0) ? Utils.GetMainDisplayIndex() : value);
		PlayerPrefs.SetInt("displayIndex", DisplayIndex);
		await Screen.MoveMainWindowTo(displayInfos[DisplayIndex], Vector2Int.zero);
		int defaultResolutionIndex = Utils.GetDefaultResolutionIndex(displayInfos[DisplayIndex]);
		int value2 = ((!isInitialLoad) ? defaultResolutionIndex : ((ResolutionIndex > Screen.resolutions.Length - 1 || ResolutionIndex < 0) ? defaultResolutionIndex : ResolutionIndex));
		UpdateResolutionIndex(value2);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnDisplayIndexChanged", new Dictionary<string, object>
		{
			{ "displayIndex", DisplayIndex },
			{ "resolutionIndex", ResolutionIndex }
		});
	}

	public void UpdateResolutionIndex(int value)
	{
		ResolutionIndex = value;
		PlayerPrefs.SetInt("resolutionIndex", ResolutionIndex);
		Screen.SetResolution(Screen.resolutions[ResolutionIndex].width, Screen.resolutions[ResolutionIndex].height, Screen.fullScreenMode, Screen.resolutions[ResolutionIndex].refreshRateRatio);
	}

	public void UpdateVSync(bool value)
	{
		VSync = (value ? 1 : 0);
		PlayerPrefs.SetInt("vSync", VSync);
		if (value)
		{
			QualitySettings.vSyncCount = 1;
		}
		else
		{
			QualitySettings.vSyncCount = 0;
		}
	}

	public void UpdateFpsLimit(int value)
	{
		FpsLimit = value;
		PlayerPrefs.SetInt("fpsLimit", FpsLimit);
		Application.targetFrameRate = FpsLimit;
	}

	public void UpdateFov(float value)
	{
		Fov = value;
		PlayerPrefs.SetFloat("fov", Fov);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnFovChanged", new Dictionary<string, object> { { "value", Fov } });
	}

	public void UpdateQuality(string value)
	{
		Quality = value;
		PlayerPrefs.SetString("quality", Quality);
		switch (Quality)
		{
		case "LOW":
			QualitySettings.SetQualityLevel(0, applyExpensiveChanges: true);
			break;
		case "MEDIUM":
			QualitySettings.SetQualityLevel(1, applyExpensiveChanges: true);
			break;
		case "HIGH":
			QualitySettings.SetQualityLevel(2, applyExpensiveChanges: true);
			break;
		case "ULTRA":
			QualitySettings.SetQualityLevel(3, applyExpensiveChanges: true);
			break;
		}
		UpdateVSync(VSync > 0);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnQualityChanged", new Dictionary<string, object> { { "value", Quality } });
	}

	public void UpdateMotionBlur(bool value)
	{
		MotionBlur = (value ? 1 : 0);
		PlayerPrefs.SetInt("motionBlur", MotionBlur);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMotionBlurChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateCountry(string value)
	{
		Country = value;
		PlayerPrefs.SetString("country", Country);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnCountryChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateVisorSkin(PlayerTeam team, PlayerRole role, string value)
	{
		switch (role)
		{
		case PlayerRole.Attacker:
			switch (team)
			{
			case PlayerTeam.Blue:
				VisorAttackerBlueSkin = value;
				PlayerPrefs.SetString("visorAttackerBlueSkin", VisorAttackerBlueSkin);
				break;
			case PlayerTeam.Red:
				VisorAttackerRedSkin = value;
				PlayerPrefs.SetString("visorAttackerRedSkin", VisorAttackerRedSkin);
				break;
			}
			break;
		case PlayerRole.Goalie:
			switch (team)
			{
			case PlayerTeam.Blue:
				VisorGoalieBlueSkin = value;
				PlayerPrefs.SetString("visorGoalieBlueSkin", VisorGoalieBlueSkin);
				break;
			case PlayerTeam.Red:
				VisorGoalieRedSkin = value;
				PlayerPrefs.SetString("visorGoalieRedSkin", VisorGoalieRedSkin);
				break;
			}
			break;
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnVisorSkinChanged", new Dictionary<string, object>
		{
			{ "team", team },
			{ "role", role },
			{ "value", value }
		});
	}

	public void UpdateMustache(string value)
	{
		Mustache = value;
		PlayerPrefs.SetString("mustache", Mustache);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnMustacheChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateBeard(string value)
	{
		Beard = value;
		PlayerPrefs.SetString("beard", Beard);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnBeardChanged", new Dictionary<string, object> { { "value", value } });
	}

	public void UpdateJerseySkin(PlayerTeam team, PlayerRole role, string value)
	{
		switch (role)
		{
		case PlayerRole.Attacker:
			switch (team)
			{
			case PlayerTeam.Blue:
				JerseyAttackerBlueSkin = value;
				PlayerPrefs.SetString("jerseyAttackerBlueSkin", JerseyAttackerBlueSkin);
				break;
			case PlayerTeam.Red:
				JerseyAttackerRedSkin = value;
				PlayerPrefs.SetString("jerseyAttackerRedSkin", JerseyAttackerRedSkin);
				break;
			}
			break;
		case PlayerRole.Goalie:
			switch (team)
			{
			case PlayerTeam.Blue:
				JerseyGoalieBlueSkin = value;
				PlayerPrefs.SetString("jerseyGoalieBlueSkin", JerseyGoalieBlueSkin);
				break;
			case PlayerTeam.Red:
				JerseyGoalieRedSkin = value;
				PlayerPrefs.SetString("jerseyGoalieRedSkin", JerseyGoalieRedSkin);
				break;
			}
			break;
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnJerseySkinChanged", new Dictionary<string, object>
		{
			{ "team", team },
			{ "role", role },
			{ "value", value }
		});
	}

	public void UpdateStickSkin(PlayerTeam team, PlayerRole role, string value)
	{
		switch (role)
		{
		case PlayerRole.Attacker:
			switch (team)
			{
			case PlayerTeam.Blue:
				StickAttackerBlueSkin = value;
				PlayerPrefs.SetString("stickAttackerBlueSkin", StickAttackerBlueSkin);
				break;
			case PlayerTeam.Red:
				StickAttackerRedSkin = value;
				PlayerPrefs.SetString("stickAttackerRedSkin", StickAttackerRedSkin);
				break;
			}
			break;
		case PlayerRole.Goalie:
			switch (team)
			{
			case PlayerTeam.Blue:
				StickGoalieBlueSkin = value;
				PlayerPrefs.SetString("stickGoalieBlueSkin", StickGoalieBlueSkin);
				break;
			case PlayerTeam.Red:
				StickGoalieRedSkin = value;
				PlayerPrefs.SetString("stickGoalieRedSkin", StickGoalieRedSkin);
				break;
			}
			break;
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnStickSkinChanged", new Dictionary<string, object>
		{
			{ "team", team },
			{ "role", role },
			{ "value", value }
		});
	}

	public void UpdateStickShaftSkin(PlayerTeam team, PlayerRole role, string value)
	{
		switch (role)
		{
		case PlayerRole.Attacker:
			switch (team)
			{
			case PlayerTeam.Blue:
				StickShaftAttackerBlueTapeSkin = value;
				PlayerPrefs.SetString("stickShaftAttackerBlueTapeSkin", StickShaftAttackerBlueTapeSkin);
				break;
			case PlayerTeam.Red:
				StickShaftAttackerRedTapeSkin = value;
				PlayerPrefs.SetString("stickShaftAttackerRedTapeSkin", StickShaftAttackerRedTapeSkin);
				break;
			}
			break;
		case PlayerRole.Goalie:
			switch (team)
			{
			case PlayerTeam.Blue:
				StickShaftGoalieBlueTapeSkin = value;
				PlayerPrefs.SetString("stickShaftGoalieBlueTapeSkin", StickShaftGoalieBlueTapeSkin);
				break;
			case PlayerTeam.Red:
				StickShaftGoalieRedTapeSkin = value;
				PlayerPrefs.SetString("stickShaftGoalieRedTapeSkin", StickShaftGoalieRedTapeSkin);
				break;
			}
			break;
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnStickShaftTapeSkinChanged", new Dictionary<string, object>
		{
			{ "team", team },
			{ "role", role },
			{ "value", value }
		});
	}

	public void UpdateStickBladeSkin(PlayerTeam team, PlayerRole role, string value)
	{
		switch (role)
		{
		case PlayerRole.Attacker:
			switch (team)
			{
			case PlayerTeam.Blue:
				StickBladeAttackerBlueTapeSkin = value;
				PlayerPrefs.SetString("stickBladeAttackerBlueTapeSkin", StickBladeAttackerBlueTapeSkin);
				break;
			case PlayerTeam.Red:
				StickBladeAttackerRedTapeSkin = value;
				PlayerPrefs.SetString("stickBladeAttackerRedTapeSkin", StickBladeAttackerRedTapeSkin);
				break;
			}
			break;
		case PlayerRole.Goalie:
			switch (team)
			{
			case PlayerTeam.Blue:
				StickBladeGoalieBlueTapeSkin = value;
				PlayerPrefs.SetString("stickBladeGoalieBlueTapeSkin", StickBladeGoalieBlueTapeSkin);
				break;
			case PlayerTeam.Red:
				StickBladeGoalieRedTapeSkin = value;
				PlayerPrefs.SetString("stickBladeGoalieRedTapeSkin", StickBladeGoalieRedTapeSkin);
				break;
			}
			break;
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnStickBladeTapeSkinChanged", new Dictionary<string, object>
		{
			{ "team", team },
			{ "role", role },
			{ "value", value }
		});
	}
}
