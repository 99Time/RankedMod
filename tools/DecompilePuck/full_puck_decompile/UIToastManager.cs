using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIToastManager : UIComponent<UIToastManager>
{
	[Header("Components")]
	[SerializeField]
	public VisualTreeAsset toastAsset;

	private Dictionary<string, Toast> activeToasts = new Dictionary<string, Toast>();

	public override void Awake()
	{
		base.Awake();
		base.AlwaysVisible = true;
	}

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("ToastsContainer");
		container.Clear();
	}

	public void ShowToast(string name, string content, float hideDelay = 3f)
	{
		if (!Application.isBatchMode)
		{
			if (activeToasts.ContainsKey(name))
			{
				HideToast(name);
			}
			TemplateContainer templateContainer = Utils.InstantiateVisualTreeAsset(toastAsset, Position.Relative);
			VisualElement visualElement = templateContainer.Query<VisualElement>("Toast");
			Toast toast = new Toast(this, templateContainer, visualElement, name, content, hideDelay);
			container.Add(toast.TemplateContainer);
			activeToasts.Add(name, toast);
		}
	}

	public void HideToast(string name)
	{
		if (activeToasts.ContainsKey(name))
		{
			container.Remove(activeToasts[name].TemplateContainer);
			activeToasts[name].Dispose();
			activeToasts.Remove(name);
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
		return "UIToastManager";
	}
}
