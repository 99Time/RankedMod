using System.Collections.Generic;
using UnityEngine.UIElements;

public class Popup
{
	public TemplateContainer TemplateContainer;

	public VisualElement VisualElement;

	public string Name;

	public string Title;

	public object Content;

	public bool ShowOkButton;

	public bool ShowCloseButton;

	private Label titleLabel;

	private VisualElement contentContainerVisualElement;

	private Button okButton;

	private Button closeButton;

	public Popup(TemplateContainer templateContainer, VisualElement visualElement, string name, string title, object content, bool showOkButton, bool showCloseButton)
	{
		TemplateContainer = templateContainer;
		VisualElement = visualElement;
		Name = name;
		Title = title;
		Content = content;
		ShowOkButton = showOkButton;
		ShowCloseButton = showCloseButton;
		Initialize();
	}

	public void Initialize()
	{
		titleLabel = VisualElement.Query<Label>("TitleLabel");
		titleLabel.text = Title;
		contentContainerVisualElement = VisualElement.Query<VisualElement>("ContentContainer");
		okButton = VisualElement.Query<Button>("OkButton");
		closeButton = VisualElement.Query<Button>("CloseButton");
		okButton.clicked += OnClickOk;
		closeButton.clicked += OnClickClose;
		if (!ShowOkButton)
		{
			okButton.style.display = DisplayStyle.None;
		}
		if (!ShowCloseButton)
		{
			closeButton.style.display = DisplayStyle.None;
		}
		if (Content is IPopupContent popupContent)
		{
			popupContent.Initialize(contentContainerVisualElement);
		}
	}

	public void Dispose()
	{
		okButton.clicked -= OnClickOk;
		closeButton.clicked -= OnClickClose;
		if (Content is IPopupContent popupContent)
		{
			popupContent.Dispose(contentContainerVisualElement);
		}
	}

	private void OnClickOk()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPopupClickOk", new Dictionary<string, object>
		{
			{ "name", Name },
			{ "content", Content }
		});
	}

	private void OnClickClose()
	{
		MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPopupClickClose", new Dictionary<string, object>
		{
			{ "name", Name },
			{ "content", Content }
		});
	}
}
