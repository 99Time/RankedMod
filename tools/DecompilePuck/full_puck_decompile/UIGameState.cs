using System;
using UnityEngine;
using UnityEngine.UIElements;

public class UIGameState : UIComponent<UIGameState>
{
	private Label gameTimeLabel;

	private Label gamePhaseLabel;

	private Label blueScoreLabel;

	private Label redScoreLabel;

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("GameStateContainer");
		gameTimeLabel = container.Query<Label>("GameTime");
		gamePhaseLabel = container.Query<Label>("GamePhase");
		blueScoreLabel = container.Query<Label>("BlueScore");
		redScoreLabel = container.Query<Label>("RedScore");
	}

	public override void Show()
	{
		if (MonoBehaviourSingleton<SettingsManager>.Instance.ShowGameUserInterface != 0)
		{
			base.Show();
		}
	}

	public void SetBlueTeamScore(int score)
	{
		if (!Application.isBatchMode)
		{
			blueScoreLabel.text = $"{score}";
		}
	}

	public void SetRedTeamScore(int score)
	{
		if (!Application.isBatchMode)
		{
			redScoreLabel.text = $"{score}";
		}
	}

	public void SetGameTime(float time)
	{
		if (!Application.isBatchMode)
		{
			TimeSpan timeSpan = TimeSpan.FromSeconds(time);
			gameTimeLabel.text = timeSpan.ToString("mm':'ss") ?? "";
		}
	}

	public void SetGamePhase(string text)
	{
		if (!Application.isBatchMode)
		{
			gamePhaseLabel.text = text;
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
		return "UIGameState";
	}
}
