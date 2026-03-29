using System;
using UnityEngine;
using UnityEngine.UIElements;

public interface UIComponent
{
	bool FocusRequiresMouse { get; set; }

	bool VisibilityRequiresMouse { get; set; }

	bool AlwaysVisible { get; set; }

	bool IsVisible { get; }

	bool IsFocused { get; set; }

	event EventHandler OnVisibilityChanged;

	event EventHandler OnFocusChanged;

	void Show();

	void Hide(bool ignoreAlwaysVisible = false);

	void Toggle();
}
public class UIComponent<T> : NetworkBehaviourSingleton<T>, UIComponent where T : NetworkBehaviourSingleton<T>
{
	private bool isFocused;

	protected VisualElement rootVisualElement;

	protected VisualElement container;

	[HideInInspector]
	public bool FocusRequiresMouse { get; set; }

	[HideInInspector]
	public bool VisibilityRequiresMouse { get; set; }

	[HideInInspector]
	public bool AlwaysVisible { get; set; }

	public bool IsVisible
	{
		get
		{
			if (container == null)
			{
				return false;
			}
			return container.style.display == DisplayStyle.Flex;
		}
	}

	public bool IsFocused
	{
		get
		{
			return isFocused;
		}
		set
		{
			isFocused = value;
			this.OnFocusChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	[HideInInspector]
	public event EventHandler OnVisibilityChanged;

	[HideInInspector]
	public event EventHandler OnFocusChanged;

	public virtual void Show()
	{
		if (!Application.isBatchMode && container != null)
		{
			container.style.display = DisplayStyle.Flex;
			this.OnVisibilityChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public virtual void Hide(bool ignoreAlwaysVisible = false)
	{
		if (!Application.isBatchMode && container != null && (ignoreAlwaysVisible || !AlwaysVisible))
		{
			container.style.display = DisplayStyle.None;
			this.OnVisibilityChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public virtual void Toggle()
	{
		if (!Application.isBatchMode)
		{
			if (IsVisible)
			{
				Hide();
			}
			else
			{
				Show();
			}
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
		return "UIComponent`1";
	}
}
