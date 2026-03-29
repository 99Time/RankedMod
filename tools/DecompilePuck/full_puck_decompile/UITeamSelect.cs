using UnityEngine.UIElements;

public class UITeamSelect : UIComponent<UITeamSelect>
{
	private Button teamBlueButton;

	private Button teamRedButton;

	private Button teamSpectatorButton;

	private void Start()
	{
		base.VisibilityRequiresMouse = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("TeamSelectContainer");
		teamBlueButton = container.Query<Button>("TeamBlueButton");
		teamBlueButton.clicked += OnClickTeamBlue;
		teamRedButton = container.Query<Button>("TeamRedButton");
		teamRedButton.clicked += OnClickTeamRed;
		teamSpectatorButton = container.Query<Button>("TeamSpectatorButton");
		teamSpectatorButton.clicked += OnClickTeamSpectator;
	}

	private void OnClickTeamBlue()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnTeamSelectClickTeamBlue");
	}

	private void OnClickTeamRed()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnTeamSelectClickTeamRed");
	}

	private void OnClickTeamSpectator()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnTeamSelectClickTeamSpectator");
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
		return "UITeamSelect";
	}
}
