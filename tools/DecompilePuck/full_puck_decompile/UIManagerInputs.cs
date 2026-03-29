using UnityEngine;
using UnityEngine.InputSystem;

public class UIManagerInputs : MonoBehaviour
{
	private UIManager uiManager;

	private void Awake()
	{
		uiManager = GetComponent<UIManager>();
	}

	private void Start()
	{
		MonoBehaviourSingleton<InputManager>.Instance.PauseAction.performed += OnPauseActionPerformed;
		MonoBehaviourSingleton<InputManager>.Instance.AllChatAction.canceled += OnAllChatActionCanceled;
		MonoBehaviourSingleton<InputManager>.Instance.TeamChatAction.canceled += OnTeamChatActionCanceled;
		MonoBehaviourSingleton<InputManager>.Instance.PositionSelectAction.performed += OnPositionSelectActionPerformed;
		MonoBehaviourSingleton<InputManager>.Instance.ScoreboardAction.started += OnScoreboardActionStarted;
		MonoBehaviourSingleton<InputManager>.Instance.ScoreboardAction.canceled += OnScoreboardActionCanceled;
		MonoBehaviourSingleton<InputManager>.Instance.QuickChat1Action.performed += OnQuickChatAction1Performed;
		MonoBehaviourSingleton<InputManager>.Instance.QuickChat2Action.performed += OnQuickChatAction2Performed;
		MonoBehaviourSingleton<InputManager>.Instance.QuickChat3Action.performed += OnQuickChatAction3Performed;
		MonoBehaviourSingleton<InputManager>.Instance.QuickChat4Action.performed += OnQuickChatAction4Performed;
	}

	private void OnDestroy()
	{
		MonoBehaviourSingleton<InputManager>.Instance.PauseAction.performed -= OnPauseActionPerformed;
		MonoBehaviourSingleton<InputManager>.Instance.AllChatAction.canceled -= OnAllChatActionCanceled;
		MonoBehaviourSingleton<InputManager>.Instance.TeamChatAction.canceled -= OnTeamChatActionCanceled;
		MonoBehaviourSingleton<InputManager>.Instance.PositionSelectAction.performed -= OnPositionSelectActionPerformed;
		MonoBehaviourSingleton<InputManager>.Instance.ScoreboardAction.started -= OnScoreboardActionStarted;
		MonoBehaviourSingleton<InputManager>.Instance.ScoreboardAction.canceled -= OnScoreboardActionCanceled;
		MonoBehaviourSingleton<InputManager>.Instance.QuickChat1Action.performed -= OnQuickChatAction1Performed;
		MonoBehaviourSingleton<InputManager>.Instance.QuickChat2Action.performed -= OnQuickChatAction2Performed;
		MonoBehaviourSingleton<InputManager>.Instance.QuickChat3Action.performed -= OnQuickChatAction3Performed;
		MonoBehaviourSingleton<InputManager>.Instance.QuickChat4Action.performed -= OnQuickChatAction4Performed;
	}

	private void OnPauseActionPerformed(InputAction.CallbackContext context)
	{
		if (uiManager.UIState == UIState.Play && !uiManager.Chat.IsFocused && !uiManager.Chat.IsQuickChatOpen && !uiManager.Settings.IsVisible)
		{
			uiManager.PauseMenu.Toggle();
		}
	}

	private void OnAllChatActionCanceled(InputAction.CallbackContext context)
	{
		if (uiManager.UIState == UIState.Play && !uiManager.PauseMenu.IsVisible && !uiManager.Chat.IsFocused && !uiManager.Chat.IsQuickChatOpen && !uiManager.Settings.IsVisible)
		{
			uiManager.Chat.Focus();
			uiManager.Chat.UseTeamChat = false;
		}
	}

	private void OnTeamChatActionCanceled(InputAction.CallbackContext context)
	{
		if (uiManager.UIState == UIState.Play && !uiManager.PauseMenu.IsVisible && !uiManager.Chat.IsFocused && !uiManager.Chat.IsQuickChatOpen && !uiManager.Settings.IsVisible)
		{
			uiManager.Chat.Focus();
			uiManager.Chat.UseTeamChat = true;
		}
	}

	private void OnPositionSelectActionPerformed(InputAction.CallbackContext context)
	{
		if (uiManager.UIState == UIState.Play && !uiManager.PauseMenu.IsVisible && !uiManager.Chat.IsFocused && !uiManager.Chat.IsQuickChatOpen && !uiManager.TeamSelect.IsVisible && !uiManager.PositionSelect.IsVisible)
		{
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerRequestPositionSelect");
		}
	}

	private void OnScoreboardActionStarted(InputAction.CallbackContext context)
	{
		if (uiManager.UIState == UIState.Play && !uiManager.PauseMenu.IsVisible && !uiManager.Chat.IsFocused && !uiManager.Settings.IsVisible)
		{
			uiManager.Scoreboard.Show();
		}
	}

	private void OnScoreboardActionCanceled(InputAction.CallbackContext context)
	{
		uiManager.Scoreboard.Hide();
	}

	private void OnQuickChatAction1Performed(InputAction.CallbackContext context)
	{
		if (uiManager.UIState == UIState.Play && !uiManager.PauseMenu.IsVisible && !uiManager.Chat.IsFocused && !uiManager.Settings.IsVisible)
		{
			uiManager.Chat.OnQuickChat(0);
		}
	}

	private void OnQuickChatAction2Performed(InputAction.CallbackContext context)
	{
		if (uiManager.UIState == UIState.Play && !uiManager.PauseMenu.IsVisible && !uiManager.Chat.IsFocused && !uiManager.Settings.IsVisible)
		{
			uiManager.Chat.OnQuickChat(1);
		}
	}

	private void OnQuickChatAction3Performed(InputAction.CallbackContext context)
	{
		if (uiManager.UIState == UIState.Play && !uiManager.PauseMenu.IsVisible && !uiManager.Chat.IsFocused && !uiManager.Settings.IsVisible)
		{
			uiManager.Chat.OnQuickChat(2);
		}
	}

	private void OnQuickChatAction4Performed(InputAction.CallbackContext context)
	{
		if (uiManager.UIState == UIState.Play && !uiManager.PauseMenu.IsVisible && !uiManager.Chat.IsFocused && !uiManager.Settings.IsVisible)
		{
			uiManager.Chat.OnQuickChat(3);
		}
	}
}
