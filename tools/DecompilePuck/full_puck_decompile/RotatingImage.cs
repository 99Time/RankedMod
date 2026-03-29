using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public class RotatingImage : VisualElement
{
	[Serializable]
	[CompilerGenerated]
	public new class UxmlSerializedData : VisualElement.UxmlSerializedData
	{
		[SerializeField]
		private float rotationSpeed;

		[SerializeField]
		[UxmlIgnore]
		[HideInInspector]
		private UxmlAttributeFlags rotationSpeed_UxmlAttributeFlags;

		[RegisterUxmlCache]
		[Conditional("UNITY_EDITOR")]
		public new static void Register()
		{
			UxmlDescriptionCache.RegisterType(typeof(UxmlSerializedData), new UxmlAttributeNames[1]
			{
				new UxmlAttributeNames("rotationSpeed", "rotation-speed", null)
			});
		}

		public override object CreateInstance()
		{
			return new RotatingImage();
		}

		public override void Deserialize(object obj)
		{
			base.Deserialize(obj);
			RotatingImage rotatingImage = (RotatingImage)obj;
			if (UnityEngine.UIElements.UxmlSerializedData.ShouldWriteAttributeValue(rotationSpeed_UxmlAttributeFlags))
			{
				rotatingImage.rotationSpeed = rotationSpeed;
			}
		}
	}

	[UxmlAttribute]
	public float rotationSpeed { get; set; }

	public RotatingImage()
	{
		RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
		RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
	}

	private void OnAttachToPanel(AttachToPanelEvent e)
	{
		base.schedule.Execute(OnScheduleUpdate).Every(16L);
	}

	private void OnDetachFromPanel(DetachFromPanelEvent e)
	{
	}

	private void OnScheduleUpdate()
	{
		base.style.rotate = new Rotate(base.style.rotate.value.angle.value + rotationSpeed);
	}
}
