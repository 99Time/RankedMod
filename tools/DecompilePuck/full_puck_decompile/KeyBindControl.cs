using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public class KeyBindControl : VisualElement
{
	[Serializable]
	[CompilerGenerated]
	public new class UxmlSerializedData : VisualElement.UxmlSerializedData
	{
		[SerializeField]
		private string NameLabel;

		[SerializeField]
		[UxmlIgnore]
		[HideInInspector]
		private UxmlAttributeFlags NameLabel_UxmlAttributeFlags;

		[SerializeField]
		private string PathLabel;

		[SerializeField]
		[UxmlIgnore]
		[HideInInspector]
		private UxmlAttributeFlags PathLabel_UxmlAttributeFlags;

		[SerializeField]
		private string TypeDropdownValue;

		[SerializeField]
		[UxmlIgnore]
		[HideInInspector]
		private UxmlAttributeFlags TypeDropdownValue_UxmlAttributeFlags;

		[SerializeField]
		private bool IsTypeDropdownVisible;

		[SerializeField]
		[UxmlIgnore]
		[HideInInspector]
		private UxmlAttributeFlags IsTypeDropdownVisible_UxmlAttributeFlags;

		[SerializeField]
		private bool IsPressable;

		[SerializeField]
		[UxmlIgnore]
		[HideInInspector]
		private UxmlAttributeFlags IsPressable_UxmlAttributeFlags;

		[SerializeField]
		private bool IsHoldable;

		[SerializeField]
		[UxmlIgnore]
		[HideInInspector]
		private UxmlAttributeFlags IsHoldable_UxmlAttributeFlags;

		[RegisterUxmlCache]
		[Conditional("UNITY_EDITOR")]
		public new static void Register()
		{
			UxmlDescriptionCache.RegisterType(typeof(UxmlSerializedData), new UxmlAttributeNames[6]
			{
				new UxmlAttributeNames("NameLabel", "name-label", null),
				new UxmlAttributeNames("PathLabel", "path-label", null),
				new UxmlAttributeNames("TypeDropdownValue", "type-dropdown-value", null),
				new UxmlAttributeNames("IsTypeDropdownVisible", "is-type-dropdown-visible", null),
				new UxmlAttributeNames("IsPressable", "is-pressable", null),
				new UxmlAttributeNames("IsHoldable", "is-holdable", null)
			});
		}

		public override object CreateInstance()
		{
			return new KeyBindControl();
		}

		public override void Deserialize(object obj)
		{
			base.Deserialize(obj);
			KeyBindControl keyBindControl = (KeyBindControl)obj;
			if (UnityEngine.UIElements.UxmlSerializedData.ShouldWriteAttributeValue(NameLabel_UxmlAttributeFlags))
			{
				keyBindControl.NameLabel = NameLabel;
			}
			if (UnityEngine.UIElements.UxmlSerializedData.ShouldWriteAttributeValue(PathLabel_UxmlAttributeFlags))
			{
				keyBindControl.PathLabel = PathLabel;
			}
			if (UnityEngine.UIElements.UxmlSerializedData.ShouldWriteAttributeValue(TypeDropdownValue_UxmlAttributeFlags))
			{
				keyBindControl.TypeDropdownValue = TypeDropdownValue;
			}
			if (UnityEngine.UIElements.UxmlSerializedData.ShouldWriteAttributeValue(IsTypeDropdownVisible_UxmlAttributeFlags))
			{
				keyBindControl.IsTypeDropdownVisible = IsTypeDropdownVisible;
			}
			if (UnityEngine.UIElements.UxmlSerializedData.ShouldWriteAttributeValue(IsPressable_UxmlAttributeFlags))
			{
				keyBindControl.IsPressable = IsPressable;
			}
			if (UnityEngine.UIElements.UxmlSerializedData.ShouldWriteAttributeValue(IsHoldable_UxmlAttributeFlags))
			{
				keyBindControl.IsHoldable = IsHoldable;
			}
		}
	}

	private string nameLabel;

	private string pathLabel;

	private string typeDropdownValue;

	private bool isTypeDropdownVisible;

	private bool isPressable = true;

	private bool isHoldable;

	public Action OnClicked;

	public Action<string> OnTypeDropdownValueChanged;

	public Label Label;

	public TextField TextField;

	public DropdownField DropdownField;

	[UxmlAttribute]
	public string NameLabel
	{
		get
		{
			return nameLabel;
		}
		set
		{
			if (!(nameLabel == value))
			{
				nameLabel = value;
				OnNameLabelChanged();
			}
		}
	}

	[UxmlAttribute]
	public string PathLabel
	{
		get
		{
			return pathLabel;
		}
		set
		{
			if (!(pathLabel == value))
			{
				pathLabel = value;
				OnPathLabelChanged();
			}
		}
	}

	[UxmlAttribute]
	public string TypeDropdownValue
	{
		get
		{
			return typeDropdownValue;
		}
		set
		{
			if (!(typeDropdownValue == value))
			{
				typeDropdownValue = value;
				if (DropdownField != null)
				{
					DropdownField.value = value;
				}
			}
		}
	}

	[UxmlAttribute]
	public bool IsTypeDropdownVisible
	{
		get
		{
			return isTypeDropdownVisible;
		}
		set
		{
			if (isTypeDropdownVisible != value)
			{
				isTypeDropdownVisible = value;
				OnTypeDropdownVisibilityChanged();
			}
		}
	}

	[UxmlAttribute]
	public bool IsPressable
	{
		get
		{
			return isPressable;
		}
		set
		{
			if (isPressable != value)
			{
				isPressable = value;
				OnControlTypeChanged();
			}
		}
	}

	[UxmlAttribute]
	public bool IsHoldable
	{
		get
		{
			return isHoldable;
		}
		set
		{
			if (isHoldable != value)
			{
				isHoldable = value;
				OnControlTypeChanged();
			}
		}
	}

	public KeyBindControl()
	{
		RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
		RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
	}

	private void OnAttachToPanel(AttachToPanelEvent e)
	{
		Label = this.Query<Label>("Label").First();
		TextField = this.Query<TextField>("TextField").First();
		if (TextField != null)
		{
			TextField.RegisterCallback<ClickEvent>(OnTextFieldClickEvent);
		}
		DropdownField = this.Query<DropdownField>("DropdownField").First();
		if (DropdownField != null)
		{
			DropdownField.RegisterValueChangedCallback(OnDropdownFieldValueChanged);
		}
		OnNameLabelChanged();
		OnPathLabelChanged();
		OnControlTypeChanged();
		OnTypeDropdownVisibilityChanged();
	}

	private void OnDetachFromPanel(DetachFromPanelEvent e)
	{
	}

	private void OnNameLabelChanged()
	{
		if (Label != null)
		{
			Label.text = nameLabel;
		}
	}

	private void OnPathLabelChanged()
	{
		if (TextField != null)
		{
			TextField.value = PathLabel;
		}
	}

	private void OnTypeDropdownVisibilityChanged()
	{
		if (DropdownField != null)
		{
			DropdownField.style.display = ((!IsTypeDropdownVisible) ? DisplayStyle.None : DisplayStyle.Flex);
		}
	}

	private void OnTextFieldClickEvent(ClickEvent clickEvent)
	{
		OnClicked?.Invoke();
	}

	private void OnDropdownFieldValueChanged(ChangeEvent<string> changeEvent)
	{
		OnTypeDropdownValueChanged?.Invoke(changeEvent.newValue);
	}

	private void OnControlTypeChanged()
	{
		if (DropdownField != null)
		{
			if (IsPressable)
			{
				DropdownField.choices = new List<string> { "PRESS", "RELEASE", "DOUBLE PRESS", "HOLD" };
				DropdownField.index = 0;
			}
			else if (IsHoldable)
			{
				DropdownField.choices = new List<string> { "CONTINUOUS", "TOGGLE" };
				DropdownField.index = 0;
			}
			else
			{
				DropdownField.choices = new List<string>();
				DropdownField.index = -1;
			}
		}
	}
}
