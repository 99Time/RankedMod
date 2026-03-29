using UnityEngine.UIElements;

public class UIPlayerMenu : UIComponent<UIPlayerMenu>
{
	private Button identityButton;

	private Button appearanceButton;

	private Button statisticsButton;

	private Button backButton;

	private void Start()
	{
		base.VisibilityRequiresMouse = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("PlayerMenuContainer");
		identityButton = container.Query<Button>("IdentityButton");
		identityButton.clicked += OnClickIdentity;
		appearanceButton = container.Query<Button>("AppearanceButton");
		appearanceButton.clicked += OnClickAppearance;
		statisticsButton = container.Query<Button>("StatisticsButton");
		statisticsButton.clicked += OnClickStatistics;
		backButton = container.Query<Button>("BackButton");
		backButton.clicked += OnClickBack;
	}

	public override void Show()
	{
		base.Show();
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerMenuShow");
	}

	public override void Hide(bool ignoreAlwaysVisible = false)
	{
		base.Hide(ignoreAlwaysVisible);
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerMenuHide");
	}

	private void OnClickIdentity()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerMenuClickIdentity");
	}

	private void OnClickAppearance()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerMenuClickAppearance");
	}

	private void OnClickStatistics()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerMenuClickStatistics");
	}

	private void OnClickBack()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPlayerMenuClickBack");
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
		return "UIPlayerMenu";
	}
}
