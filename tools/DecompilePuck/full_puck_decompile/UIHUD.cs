using System;
using UnityEngine;
using UnityEngine.UIElements;

public class UIHUD : UIComponent<UIHUD>
{
	private ProgressBar staminaProgressBar;

	private Label speedLabel;

	private Label unitsLabel;

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("PlayerContainer");
		staminaProgressBar = container.Query<ProgressBar>("StaminaProgressBar");
		speedLabel = container.Query<Label>("SpeedLabel");
		unitsLabel = container.Query<Label>("UnitsLabel");
	}

	public void SetStamina(float value)
	{
		if (!Application.isBatchMode)
		{
			staminaProgressBar.value = value;
		}
	}

	public void SetSpeed(float value)
	{
		if (!Application.isBatchMode)
		{
			float num = (float)Math.Round((MonoBehaviourSingleton<SettingsManager>.Instance.Units == "METRIC") ? Utils.GameUnitsToMetric(value) : Utils.GameUnitsToImperial(value), 1);
			speedLabel.text = num.ToString("F1");
		}
	}

	public void SetUnits(string units)
	{
		if (!Application.isBatchMode)
		{
			unitsLabel.text = units;
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
		return "UIHUD";
	}
}
