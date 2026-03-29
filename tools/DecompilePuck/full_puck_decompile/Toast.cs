using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Toast
{
	public UIToastManager UIToastManager;

	public TemplateContainer TemplateContainer;

	public VisualElement VisualElement;

	public string Name;

	public string Content;

	public float HideDelay;

	private IEnumerator hideCoroutine;

	private Label contentLabel;

	public Toast(UIToastManager uiToastManager, TemplateContainer templateContainer, VisualElement visualElement, string name, string content, float hideDelay)
	{
		UIToastManager = uiToastManager;
		TemplateContainer = templateContainer;
		VisualElement = visualElement;
		Name = name;
		Content = content;
		HideDelay = hideDelay;
		Initialize();
	}

	public void Initialize()
	{
		VisualElement.RegisterCallback<ClickEvent>(OnClick);
		contentLabel = VisualElement.Query<Label>("ContentLabel");
		contentLabel.text = Content;
		Hide();
	}

	public void Hide()
	{
		hideCoroutine = IHide();
		UIToastManager.StartCoroutine(hideCoroutine);
	}

	private IEnumerator IHide()
	{
		yield return new WaitForSeconds(HideDelay);
		UIToastManager.HideToast(Name);
		hideCoroutine = null;
	}

	public void Dispose()
	{
		VisualElement.UnregisterCallback<ClickEvent>(OnClick);
		if (hideCoroutine != null)
		{
			UIToastManager.StopCoroutine(hideCoroutine);
			hideCoroutine = null;
		}
	}

	private void OnClick(ClickEvent clickEvent)
	{
		UIToastManager.HideToast(Name);
	}
}
