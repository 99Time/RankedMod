using UnityEngine.UIElements;

public class UIDebug : UIComponent<UIDebug>
{
	private Label buildLabel;

	public override void Awake()
	{
		base.Awake();
		base.AlwaysVisible = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("DebugContainer");
		buildLabel = container.Query<Label>("BuildLabel");
	}

	public void SetBuildLabelText(string text)
	{
		buildLabel.text = text;
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
		return "UIDebug";
	}
}
