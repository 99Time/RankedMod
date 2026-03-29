using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.UIElements;

public class UIIdentity : UIComponent<UIIdentity>
{
	private TextField nameTextField;

	private TextField numberTextField;

	private Button confirmButton;

	private Button closeButton;

	private string username;

	private int number;

	private void Start()
	{
		base.VisibilityRequiresMouse = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("IdentityContainer");
		confirmButton = container.Query<Button>("ConfirmButton");
		confirmButton.clicked += OnClickConfirm;
		closeButton = container.Query<Button>("CloseButton");
		closeButton.clicked += OnClickClose;
		nameTextField = container.Query<VisualElement>("NameTextField").First().Query<TextField>("TextField");
		nameTextField.RegisterValueChangedCallback(OnNameChanged);
		nameTextField.RegisterCallback<FocusOutEvent>(OnNameFocusOut);
		numberTextField = container.Query<VisualElement>("NumberTextField").First().Query<TextField>("TextField");
		numberTextField.RegisterValueChangedCallback(OnNumberChanged);
	}

	public void ApplyIdentityValues()
	{
		nameTextField.value = MonoBehaviourSingleton<StateManager>.Instance.PlayerData.username;
		numberTextField.value = MonoBehaviourSingleton<StateManager>.Instance.PlayerData.number.ToString();
	}

	public override void Show()
	{
		base.Show();
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnIdentityShow");
	}

	public override void Hide(bool ignoreAlwaysVisible = false)
	{
		base.Hide(ignoreAlwaysVisible);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnIdentityHide");
	}

	private void ResetName()
	{
		username = "PLAYER";
		nameTextField.value = "PLAYER";
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnIdentityNameChanged", new Dictionary<string, object> { { "value", username } });
	}

	private void OnNameChanged(ChangeEvent<string> changeEvent)
	{
		username = changeEvent.newValue;
		if (!string.IsNullOrEmpty(username))
		{
			username = Utils.FilterStringSpecialCharacters(username);
			username = Utils.FilterStringNotLetters(username);
			nameTextField.value = username;
		}
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnIdentityNameChanged", new Dictionary<string, object> { { "value", username } });
	}

	private void OnNameFocusOut(FocusOutEvent focusOutEvent)
	{
		if (string.IsNullOrEmpty(username))
		{
			ResetName();
			return;
		}
		username = Utils.FilterStringSpecialCharacters(username);
		username = Utils.FilterStringNotLetters(username);
		username = Utils.FilterStringProfanity(username);
		if (string.IsNullOrEmpty(username))
		{
			ResetName();
			return;
		}
		nameTextField.value = username;
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnIdentityNameChanged", new Dictionary<string, object> { { "value", username } });
	}

	private void OnNumberChanged(ChangeEvent<string> changeEvent)
	{
		string text = new Regex("[^0-9]").Replace(changeEvent.newValue, "");
		numberTextField.value = text;
		if (int.TryParse(text, out var result))
		{
			number = result;
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnIdentityNumberChanged", new Dictionary<string, object> { { "value", number } });
		}
	}

	private void OnClickClose()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnIdentityClickClose");
	}

	private void OnClickConfirm()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnIdentityClickConfirm");
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
		return "UIIdentity";
	}
}
