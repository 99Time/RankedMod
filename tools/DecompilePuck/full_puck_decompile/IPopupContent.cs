using UnityEngine.UIElements;

internal interface IPopupContent
{
	void Initialize(VisualElement containerVisualElement);

	void Dispose(VisualElement containerVisualElement);
}
