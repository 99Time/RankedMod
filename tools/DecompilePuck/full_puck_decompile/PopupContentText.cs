using UnityEngine.UIElements;

public class PopupContentText : IPopupContent
{
	public VisualTreeAsset Asset;

	public string Text;

	private TemplateContainer templateContainer;

	public PopupContentText(VisualTreeAsset asset, string text)
	{
		Asset = asset;
		Text = text;
	}

	public void Initialize(VisualElement containerVisualElement)
	{
		templateContainer = Utils.InstantiateVisualTreeAsset(Asset, Position.Relative);
		((Label)templateContainer.Query<Label>("TextLabel")).text = Text;
		containerVisualElement.Add(templateContainer);
	}

	public void Dispose(VisualElement containerVisualElement)
	{
		containerVisualElement.Remove(templateContainer);
	}
}
