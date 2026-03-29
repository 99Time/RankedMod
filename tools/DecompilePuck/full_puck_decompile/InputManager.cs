using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviourSingleton<InputManager>
{
	private UnityEngine.InputSystem.PlayerInput playerInput;

	private InputActionAsset actions;

	[HideInInspector]
	public InputAction MoveForwardAction;

	[HideInInspector]
	public InputAction MoveBackwardAction;

	[HideInInspector]
	public InputAction TurnLeftAction;

	[HideInInspector]
	public InputAction TurnRightAction;

	[HideInInspector]
	public InputAction StickAction;

	[HideInInspector]
	public InputAction BladeAngleUpAction;

	[HideInInspector]
	public InputAction BladeAngleDownAction;

	[HideInInspector]
	public InputAction SlideAction;

	[HideInInspector]
	public InputAction SprintAction;

	[HideInInspector]
	public InputAction TrackAction;

	[HideInInspector]
	public InputAction LookAction;

	[HideInInspector]
	public InputAction JumpAction;

	[HideInInspector]
	public InputAction StopAction;

	[HideInInspector]
	public InputAction TwistLeftAction;

	[HideInInspector]
	public InputAction TwistRightAction;

	[HideInInspector]
	public InputAction DashLeftAction;

	[HideInInspector]
	public InputAction DashRightAction;

	[HideInInspector]
	public InputAction ExtendLeftAction;

	[HideInInspector]
	public InputAction ExtendRightAction;

	[HideInInspector]
	public InputAction LateralLeftAction;

	[HideInInspector]
	public InputAction LateralRightAction;

	[HideInInspector]
	public InputAction TalkAction;

	[HideInInspector]
	public InputAction AllChatAction;

	[HideInInspector]
	public InputAction TeamChatAction;

	[HideInInspector]
	public InputAction PauseAction;

	[HideInInspector]
	public InputAction PositionSelectAction;

	[HideInInspector]
	public InputAction ScoreboardAction;

	[HideInInspector]
	public InputAction QuickChat1Action;

	[HideInInspector]
	public InputAction QuickChat2Action;

	[HideInInspector]
	public InputAction QuickChat3Action;

	[HideInInspector]
	public InputAction QuickChat4Action;

	[HideInInspector]
	public InputAction DebugAction;

	[HideInInspector]
	public InputAction DebugInputsAction;

	[HideInInspector]
	public InputAction DebugTackleAction;

	[HideInInspector]
	public InputAction DebugGameStateAction;

	[HideInInspector]
	public InputAction DebugShootAction;

	[HideInInspector]
	public InputAction PointAction;

	[HideInInspector]
	public InputAction ClickAction;

	public Dictionary<string, InputAction> InputActions = new Dictionary<string, InputAction>();

	public Dictionary<string, InputAction> RebindableInputActions = new Dictionary<string, InputAction>();

	public Dictionary<string, KeyBind> KeyBinds = new Dictionary<string, KeyBind>();

	public override void Awake()
	{
		base.Awake();
		playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
		actions = playerInput.actions;
		InputSystem.RegisterInteraction<DoublePressInteraction>();
		InputSystem.RegisterInteraction<ToggleInteraction>();
		MoveForwardAction = actions.FindAction("Move Forward");
		MoveBackwardAction = actions.FindAction("Move Backward");
		TurnLeftAction = actions.FindAction("Turn Left");
		TurnRightAction = actions.FindAction("Turn Right");
		StickAction = actions.FindAction("Stick");
		BladeAngleUpAction = actions.FindAction("Blade Angle Up");
		BladeAngleDownAction = actions.FindAction("Blade Angle Down");
		SlideAction = actions.FindAction("Slide");
		SprintAction = actions.FindAction("Sprint");
		TrackAction = actions.FindAction("Track");
		LookAction = actions.FindAction("Look");
		JumpAction = actions.FindAction("Jump");
		StopAction = actions.FindAction("Stop");
		TwistLeftAction = actions.FindAction("Twist Left");
		TwistRightAction = actions.FindAction("Twist Right");
		DashLeftAction = actions.FindAction("Dash Left");
		DashRightAction = actions.FindAction("Dash Right");
		ExtendLeftAction = actions.FindAction("Extend Left");
		ExtendRightAction = actions.FindAction("Extend Right");
		LateralLeftAction = actions.FindAction("Lateral Left");
		LateralRightAction = actions.FindAction("Lateral Right");
		TalkAction = actions.FindAction("Talk");
		AllChatAction = actions.FindAction("All Chat");
		TeamChatAction = actions.FindAction("Team Chat");
		PauseAction = actions.FindAction("Pause");
		PositionSelectAction = actions.FindAction("Position Select");
		ScoreboardAction = actions.FindAction("Scoreboard");
		QuickChat1Action = actions.FindAction("Quick Chat 1");
		QuickChat2Action = actions.FindAction("Quick Chat 2");
		QuickChat3Action = actions.FindAction("Quick Chat 3");
		QuickChat4Action = actions.FindAction("Quick Chat 4");
		DebugAction = actions.FindAction("Debug");
		DebugInputsAction = actions.FindAction("Debug Inputs");
		DebugTackleAction = actions.FindAction("Debug Tackle");
		DebugGameStateAction = actions.FindAction("Debug Game State");
		DebugShootAction = actions.FindAction("Debug Shoot");
		PointAction = actions.FindAction("Point");
		ClickAction = actions.FindAction("Click");
		InputActions = new Dictionary<string, InputAction>
		{
			{ "Move Forward", MoveForwardAction },
			{ "Move Backward", MoveBackwardAction },
			{ "Turn Left", TurnLeftAction },
			{ "Turn Right", TurnRightAction },
			{ "Stick", StickAction },
			{ "Blade Angle Up", BladeAngleUpAction },
			{ "Blade Angle Down", BladeAngleDownAction },
			{ "Slide", SlideAction },
			{ "Sprint", SprintAction },
			{ "Track", TrackAction },
			{ "Look", LookAction },
			{ "Jump", JumpAction },
			{ "Stop", StopAction },
			{ "Twist Left", TwistLeftAction },
			{ "Twist Right", TwistRightAction },
			{ "Dash Left", DashLeftAction },
			{ "Dash Right", DashRightAction },
			{ "Extend Left", ExtendLeftAction },
			{ "Extend Right", ExtendRightAction },
			{ "Lateral Left", LateralLeftAction },
			{ "Lateral Right", LateralRightAction },
			{ "Talk", TalkAction },
			{ "All Chat", AllChatAction },
			{ "Team Chat", TeamChatAction },
			{ "Pause", PauseAction },
			{ "Position Select", PositionSelectAction },
			{ "Scoreboard", ScoreboardAction },
			{ "Quick Chat 1", QuickChat1Action },
			{ "Quick Chat 2", QuickChat2Action },
			{ "Quick Chat 3", QuickChat3Action },
			{ "Quick Chat 4", QuickChat4Action },
			{ "Debug", DebugAction },
			{ "Debug Inputs", DebugInputsAction },
			{ "Debug Tackle", DebugTackleAction },
			{ "Debug Game State", DebugGameStateAction },
			{ "Debug Shoot", DebugShootAction },
			{ "Point", PointAction },
			{ "Click", ClickAction }
		};
		RebindableInputActions = new Dictionary<string, InputAction>
		{
			{ "Move Forward", MoveForwardAction },
			{ "Move Backward", MoveBackwardAction },
			{ "Turn Left", TurnLeftAction },
			{ "Turn Right", TurnRightAction },
			{ "Stick", StickAction },
			{ "Blade Angle Up", BladeAngleUpAction },
			{ "Blade Angle Down", BladeAngleDownAction },
			{ "Slide", SlideAction },
			{ "Sprint", SprintAction },
			{ "Track", TrackAction },
			{ "Look", LookAction },
			{ "Jump", JumpAction },
			{ "Stop", StopAction },
			{ "Twist Left", TwistLeftAction },
			{ "Twist Right", TwistRightAction },
			{ "Dash Left", DashLeftAction },
			{ "Dash Right", DashRightAction },
			{ "Extend Left", ExtendLeftAction },
			{ "Extend Right", ExtendRightAction },
			{ "Lateral Left", LateralLeftAction },
			{ "Lateral Right", LateralRightAction },
			{ "Talk", TalkAction },
			{ "All Chat", AllChatAction },
			{ "Team Chat", TeamChatAction },
			{ "Position Select", PositionSelectAction },
			{ "Scoreboard", ScoreboardAction }
		};
		Reset();
	}

	public void LoadKeyBinds()
	{
		if (Application.isBatchMode)
		{
			return;
		}
		string text = PlayerPrefs.GetString("keyBinds", null);
		if (string.IsNullOrEmpty(text))
		{
			Debug.Log("[InputManager] No keybinds founds during loading, saving default keybinds");
			SaveKeyBinds();
			return;
		}
		KeyBinds = JsonSerializer.Deserialize<Dictionary<string, KeyBind>>(text);
		Debug.Log("[InputManager] Loading keybinds: " + text);
		bool flag = false;
		foreach (KeyValuePair<string, InputAction> rebindableInputAction in RebindableInputActions)
		{
			if (KeyBinds.ContainsKey(rebindableInputAction.Key))
			{
				KeyBind keyBind = KeyBinds[rebindableInputAction.Key];
				RebindButton(rebindableInputAction.Key, keyBind.ModifierPath, keyBind.Path);
				SetActionInteractions(rebindableInputAction.Key, keyBind.Interactions);
			}
			else
			{
				flag = true;
			}
		}
		if (flag)
		{
			Debug.Log("[InputManager] Some keybinds were missing, saving default keybinds");
			SaveKeyBinds();
		}
		else
		{
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnKeyBindsLoaded", new Dictionary<string, object> { { "keyBinds", KeyBinds } });
		}
	}

	public void SaveKeyBinds()
	{
		if (Application.isBatchMode)
		{
			return;
		}
		Dictionary<string, KeyBind> dictionary = new Dictionary<string, KeyBind>();
		foreach (KeyValuePair<string, InputAction> rebindableInputAction in RebindableInputActions)
		{
			if (rebindableInputAction.Value.bindings[0].isComposite)
			{
				dictionary.Add(rebindableInputAction.Key, new KeyBind
				{
					ModifierPath = rebindableInputAction.Value.bindings[1].effectivePath,
					Path = rebindableInputAction.Value.bindings[2].effectivePath,
					Interactions = rebindableInputAction.Value.bindings[0].effectiveInteractions
				});
			}
			else
			{
				dictionary.Add(rebindableInputAction.Key, new KeyBind
				{
					ModifierPath = null,
					Path = rebindableInputAction.Value.bindings[0].effectivePath,
					Interactions = rebindableInputAction.Value.bindings[0].effectiveInteractions
				});
			}
		}
		string value = JsonSerializer.Serialize(dictionary, new JsonSerializerOptions
		{
			WriteIndented = true
		});
		PlayerPrefs.SetString("keyBinds", value);
		Debug.Log("[InputManager] Saved key binds, reloading...");
		LoadKeyBinds();
	}

	public void ResetToDefault()
	{
		PlayerPrefs.DeleteKey("keyBinds");
		foreach (KeyValuePair<string, InputAction> rebindableInputAction in RebindableInputActions)
		{
			rebindableInputAction.Value.RemoveAllBindingOverrides();
		}
		LoadKeyBinds();
	}

	public void RebindButtonInteractively(string actionName)
	{
		if (!RebindableInputActions.ContainsKey(actionName))
		{
			return;
		}
		InputAction inputAction = actions.FindAction(actionName);
		inputAction.Disable();
		string interactions = inputAction.bindings[0].effectiveInteractions;
		InputActionRebindingExtensions.RebindingOperation rebindingOperation = GenerateRebindingOperation();
		Debug.Log("[InputManager] Rebinding " + actionName + "...");
		if (inputAction.bindings[0].isComposite)
		{
			rebindingOperation.WithTargetBinding(1).OnComplete(delegate(InputActionRebindingExtensions.RebindingOperation operation)
			{
				Debug.Log("[InputManager] Rebound " + actionName + " modifierPath to " + operation.action.bindings[1].effectivePath);
				GenerateRebindingOperation().WithControlsExcluding(operation.action.bindings[1].effectivePath).WithTargetBinding(2).WithTimeout(0.5f)
					.OnComplete(delegate(InputActionRebindingExtensions.RebindingOperation rebindingOperation2)
					{
						Debug.Log("[InputManager] Rebound " + actionName + " path to " + rebindingOperation2.action.bindings[2].effectivePath);
						inputAction.Enable();
						RebindButton(actionName, rebindingOperation2.action.bindings[1].effectivePath, rebindingOperation2.action.bindings[2].effectivePath);
						SetActionInteractions(actionName, interactions);
						SaveKeyBinds();
						MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnKeyBindRebindComplete", new Dictionary<string, object> { { "actionName", actionName } });
					})
					.OnCancel(delegate(InputActionRebindingExtensions.RebindingOperation rebindingOperation2)
					{
						Debug.Log("[InputManager] Rebinding " + actionName + " path was cancelled, using modifier path as path " + rebindingOperation2.action.bindings[1].effectivePath);
						inputAction.Enable();
						RebindButton(actionName, null, rebindingOperation2.action.bindings[1].effectivePath);
						SetActionInteractions(actionName, interactions);
						SaveKeyBinds();
						MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnKeyBindRebindComplete", new Dictionary<string, object> { { "actionName", actionName } });
					})
					.Start();
			}).OnCancel(delegate
			{
				inputAction.Enable();
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnKeyBindRebindCancel", new Dictionary<string, object> { { "actionName", actionName } });
				Debug.Log("[InputManager] Rebinding " + actionName + " was cancelled");
			});
		}
		else
		{
			rebindingOperation.OnComplete(delegate(InputActionRebindingExtensions.RebindingOperation operation)
			{
				inputAction.Enable();
				RebindButton(actionName, null, operation.action.bindings[0].effectivePath);
				SetActionInteractions(actionName, interactions);
				SaveKeyBinds();
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnKeyBindRebindComplete", new Dictionary<string, object> { { "actionName", actionName } });
				Debug.Log("[InputManager] Rebound " + actionName + " to " + operation.action.bindings[0].effectivePath);
			}).OnCancel(delegate
			{
				inputAction.Enable();
				MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnKeyBindRebindCancel", new Dictionary<string, object> { { "actionName", actionName } });
				Debug.Log("[InputManager] Rebinding " + actionName + " was cancelled");
			});
		}
		rebindingOperation.Start();
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnKeyBindRebindStart", new Dictionary<string, object>
		{
			{ "actionName", actionName },
			{
				"isComposite",
				inputAction.bindings[0].isComposite
			}
		});
		InputActionRebindingExtensions.RebindingOperation GenerateRebindingOperation()
		{
			InputActionRebindingExtensions.RebindingOperation rebindingOperation2 = inputAction.PerformInteractiveRebinding().WithCancelingThrough("<Keyboard>/escape").OnMatchWaitForAnother(0.1f);
			if (inputAction.expectedControlType == "Axis")
			{
				rebindingOperation2.WithControlsExcluding("<Mouse>/scroll/y").WithControlsExcluding("<Mouse>/scroll/x");
			}
			return rebindingOperation2;
		}
	}

	public void RebindButton(string actionName, string modifierPath = null, string path = null)
	{
		if (RebindableInputActions.ContainsKey(actionName))
		{
			InputAction inputAction = actions.FindAction(actionName);
			if (inputAction.bindings[0].isComposite)
			{
				inputAction.ApplyBindingOverride(1, new InputBinding
				{
					overridePath = modifierPath
				});
				inputAction.ApplyBindingOverride(2, new InputBinding
				{
					overridePath = path
				});
			}
			else
			{
				inputAction.ApplyBindingOverride(0, new InputBinding
				{
					overridePath = path
				});
			}
		}
	}

	public void SetActionInteractions(string actionName, string interactions)
	{
		if (RebindableInputActions.ContainsKey(actionName))
		{
			InputAction inputAction = RebindableInputActions[actionName];
			if (!(inputAction.bindings[0].effectiveInteractions == interactions))
			{
				inputAction.ApplyBindingOverride(0, new InputBinding
				{
					overrideInteractions = interactions
				});
			}
		}
	}

	public void Reset()
	{
		foreach (KeyValuePair<string, InputAction> inputAction in InputActions)
		{
			inputAction.Value.Reset();
			inputAction.Value.Disable();
			inputAction.Value.Enable();
		}
		if (!Application.isEditor)
		{
			DebugInputsAction.Disable();
			DebugTackleAction.Disable();
			DebugGameStateAction.Disable();
			DebugShootAction.Disable();
		}
	}
}
