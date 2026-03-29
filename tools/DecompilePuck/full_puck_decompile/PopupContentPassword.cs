using UnityEngine.UIElements;

public class PopupContentPassword : IPopupContent
{
	public VisualTreeAsset Asset;

	public string Password;

	private TemplateContainer templateContainer;

	private TextField textField;

	public PopupContentPassword(VisualTreeAsset asset)
	{
		Asset = asset;
	}

	public void Initialize(VisualElement containerVisualElement)
	{
		templateContainer = Utils.InstantiateVisualTreeAsset(Asset, Position.Relative);
		VisualElement e = templateContainer;
		textField = e.Query<VisualElement>("PasswordTextField").First().Query<TextField>("TextField");
		textField.value = Password;
		textField.RegisterCallback<ChangeEvent<string>>(OnPasswordChanged);
		containerVisualElement.Add(templateContainer);
	}

	public void Dispose(VisualElement containerVisualElement)
	{
		textField.UnregisterCallback<ChangeEvent<string>>(OnPasswordChanged);
		containerVisualElement.Remove(templateContainer);
	}

	private void OnPasswordChanged(ChangeEvent<string> changeEvent)
	{
		Password = changeEvent.newValue;
	}
}
