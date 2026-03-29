using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIPopupManager : UIComponent<UIPopupManager>
{
	[Header("Components")]
	[SerializeField]
	public VisualTreeAsset popupAsset;

	[SerializeField]
	public VisualTreeAsset popupContentTextAsset;

	[SerializeField]
	public VisualTreeAsset popupContentPasswordAsset;

	private Dictionary<string, Popup> activePopups = new Dictionary<string, Popup>();

	public override void Awake()
	{
		base.Awake();
		base.VisibilityRequiresMouse = true;
		base.AlwaysVisible = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("PopupsContainer");
		UpdateVisibility();
	}

	public void ShowPopup(string name, string title, object content, bool showOkButton, bool showCloseButton)
	{
		if (!Application.isBatchMode && !activePopups.ContainsKey(name))
		{
			TemplateContainer templateContainer = Utils.InstantiateVisualTreeAsset(popupAsset);
			VisualElement visualElement = templateContainer.Query<VisualElement>("Popup");
			Popup popup = new Popup(templateContainer, visualElement, name, title, content, showOkButton, showCloseButton);
			container.Add(popup.TemplateContainer);
			popup.VisualElement.BringToFront();
			activePopups.Add(name, popup);
			UpdateVisibility();
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPopupShow", new Dictionary<string, object> { { "name", name } });
		}
	}

	public void HidePopup(string name)
	{
		if (activePopups.ContainsKey(name))
		{
			container.Remove(activePopups[name].TemplateContainer);
			activePopups[name].Dispose();
			activePopups.Remove(name);
			UpdateVisibility();
			MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent("Event_Client_OnPopupHide", new Dictionary<string, object> { { "name", name } });
		}
	}

	private void UpdateVisibility()
	{
		if (activePopups.Count > 0)
		{
			Show();
		}
		else
		{
			Hide(ignoreAlwaysVisible: true);
		}
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
		return "UIPopupManager";
	}
}
