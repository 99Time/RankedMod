using System;
using System.Collections.Generic;
using UnityEngine;

internal class UIPopupManagerController : MonoBehaviour
{
	private UIPopupManager uiPopupManager;

	private void Awake()
	{
		uiPopupManager = GetComponent<UIPopupManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnIdentityClickConfirm", Event_Client_OnIdentityClickConfirm);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnMainMenuClickExitGame", Event_Client_OnMainMenuClickExitGame);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPauseMenuClickExitGame", Event_Client_OnPauseMenuClickExitGame);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerBanned", Event_Client_OnPlayerBanned);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPlayerMuted", Event_Client_OnPlayerMuted);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnSettingsClickResetToDefault", Event_Client_OnSettingsClickResetToDefault);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnBeforePendingModsSet", Event_Client_OnBeforePendingModsSet);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPendingModsReset", Event_Client_OnPendingModsReset);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPendingModsCleared", Event_Client_OnPendingModsCleared);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnConnectionRejected", Event_Client_OnConnectionRejected);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPopupClickOk", Event_Client_OnPopupClickOk);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnPopupClickClose", Event_Client_OnPopupClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnKeyBindRebindStart", Event_Client_OnKeyBindRebindStart);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnKeyBindRebindComplete", Event_Client_OnKeyBindRebindComplete);
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnKeyBindRebindCancel", Event_Client_OnKeyBindRebindCancel);
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnIdentityClickConfirm", Event_Client_OnIdentityClickConfirm);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnMainMenuClickExitGame", Event_Client_OnMainMenuClickExitGame);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPauseMenuClickExitGame", Event_Client_OnPauseMenuClickExitGame);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerBanned", Event_Client_OnPlayerBanned);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPlayerMuted", Event_Client_OnPlayerMuted);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnSettingsClickResetToDefault", Event_Client_OnSettingsClickResetToDefault);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnBeforePendingModsSet", Event_Client_OnBeforePendingModsSet);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPendingModsReset", Event_Client_OnPendingModsReset);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPendingModsCleared", Event_Client_OnPendingModsCleared);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnConnectionRejected", Event_Client_OnConnectionRejected);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPopupClickOk", Event_Client_OnPopupClickOk);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnPopupClickClose", Event_Client_OnPopupClickClose);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnKeyBindRebindStart", Event_Client_OnKeyBindRebindStart);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnKeyBindRebindComplete", Event_Client_OnKeyBindRebindComplete);
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnKeyBindRebindCancel", Event_Client_OnKeyBindRebindCancel);
	}

	private void Event_Client_OnIdentityClickConfirm(Dictionary<string, object> message)
	{
		PopupContentText content = new PopupContentText(uiPopupManager.popupContentTextAsset, "<align=center>Your name and number can be changed once every 7 days. Are you sure you want to continue?");
		uiPopupManager.ShowPopup("identity", "IDENTITY", content, showOkButton: true, showCloseButton: true);
	}

	private void Event_Client_OnMainMenuClickExitGame(Dictionary<string, object> message)
	{
		PopupContentText content = new PopupContentText(uiPopupManager.popupContentTextAsset, "<align=center>Are you sure you want to exit the game?");
		uiPopupManager.ShowPopup("mainMenuExitGame", "EXIT GAME", content, showOkButton: true, showCloseButton: true);
	}

	private void Event_Client_OnPauseMenuClickExitGame(Dictionary<string, object> message)
	{
		PopupContentText content = new PopupContentText(uiPopupManager.popupContentTextAsset, "<align=center>Are you sure you want to exit the game?");
		uiPopupManager.ShowPopup("pauseMenuExitGame", "EXIT GAME", content, showOkButton: true, showCloseButton: true);
	}

	private void Event_Client_OnPlayerBanned(Dictionary<string, object> message)
	{
		string text = (string)message["reason"];
		double num = (double)message["until"];
		DateTime value = DateTime.Now.ToLocalTime();
		TimeSpan timeSpan = DateTimeOffset.FromUnixTimeMilliseconds((long)num).DateTime.ToLocalTime().Subtract(value);
		string text2 = $"{timeSpan.Days} days, {timeSpan.Hours} hours, {timeSpan.Minutes} minutes";
		PopupContentText content = new PopupContentText(uiPopupManager.popupContentTextAsset, "<align=center>Your account has been banned.<br><align=left><br><b>Reason:</b> " + text + "<br><b>Remaining:</b> " + text2);
		uiPopupManager.ShowPopup("banned", "BANNED", content, showOkButton: true, showCloseButton: true);
	}

	private void Event_Client_OnPlayerMuted(Dictionary<string, object> message)
	{
		string text = (string)message["reason"];
		double num = (double)message["until"];
		DateTime value = DateTime.Now.ToLocalTime();
		TimeSpan timeSpan = DateTimeOffset.FromUnixTimeMilliseconds((long)num).DateTime.ToLocalTime().Subtract(value);
		string text2 = $"{timeSpan.Days} days, {timeSpan.Hours} hours, {timeSpan.Minutes} minutes";
		PopupContentText content = new PopupContentText(uiPopupManager.popupContentTextAsset, "<align=center>Your account has been muted.<br><align=left><br><b>Reason:</b> " + text + "<br><b>Remaining:</b> " + text2);
		uiPopupManager.ShowPopup("muted", "MUTED", content, showOkButton: true, showCloseButton: true);
	}

	private void Event_Client_OnSettingsClickResetToDefault(Dictionary<string, object> message)
	{
		PopupContentText content = new PopupContentText(uiPopupManager.popupContentTextAsset, "<align=center>This will reset all settings to their default values, including keybinds. Are you sure you want to continue?");
		uiPopupManager.ShowPopup("settingsResetToDefault", "RESET SETTINGS", content, showOkButton: true, showCloseButton: true);
	}

	private void Event_Client_OnBeforePendingModsSet(Dictionary<string, object> message)
	{
		ulong[] array = (ulong[])message["ids"];
		PopupContentText content = new PopupContentText(uiPopupManager.popupContentTextAsset, $"<align=center>This server requires {array.Length} mods to be installed before connecting. Proceeding with installation...");
		uiPopupManager.ShowPopup("pendingMods", "MODS REQUIRED", content, showOkButton: false, showCloseButton: true);
	}

	private void Event_Client_OnPendingModsReset(Dictionary<string, object> message)
	{
		uiPopupManager.HidePopup("pendingMods");
	}

	private void Event_Client_OnPendingModsCleared(Dictionary<string, object> message)
	{
		uiPopupManager.HidePopup("pendingMods");
	}

	private void Event_Client_OnConnectionRejected(Dictionary<string, object> message)
	{
		if (((ConnectionRejection)message["connectionRejection"]).code == ConnectionRejectionCode.MissingPassword)
		{
			PopupContentPassword content = new PopupContentPassword(uiPopupManager.popupContentPasswordAsset);
			uiPopupManager.ShowPopup("missingPassword", "PASSWORD REQUIRED", content, showOkButton: true, showCloseButton: true);
		}
	}

	private void Event_Client_OnPopupClickOk(Dictionary<string, object> message)
	{
		string text = (string)message["name"];
		uiPopupManager.HidePopup(text);
		if (!(text == "mainMenuExitGame"))
		{
			if (text == "pauseMenuExitGame")
			{
				Application.Quit();
			}
		}
		else
		{
			Application.Quit();
		}
	}

	private void Event_Client_OnPopupClickClose(Dictionary<string, object> message)
	{
		string text = (string)message["name"];
		uiPopupManager.HidePopup(text);
	}

	private void Event_Client_OnKeyBindRebindStart(Dictionary<string, object> message)
	{
		if ((bool)message["isComposite"])
		{
			PopupContentText content = new PopupContentText(uiPopupManager.popupContentTextAsset, "<align=center>Press a <b>key</b> or combination of <b>modifier + key</b> to rebind.");
			uiPopupManager.ShowPopup("keyBindRebind", "KEY REBIND", content, showOkButton: false, showCloseButton: false);
		}
		else
		{
			PopupContentText content2 = new PopupContentText(uiPopupManager.popupContentTextAsset, "<align=center>Press a <b>key</b> to rebind.");
			uiPopupManager.ShowPopup("keyBindRebind", "KEY REBIND", content2, showOkButton: false, showCloseButton: false);
		}
	}

	private void Event_Client_OnKeyBindRebindComplete(Dictionary<string, object> message)
	{
		uiPopupManager.HidePopup("keyBindRebind");
	}

	private void Event_Client_OnKeyBindRebindCancel(Dictionary<string, object> message)
	{
		uiPopupManager.HidePopup("keyBindRebind");
	}
}
