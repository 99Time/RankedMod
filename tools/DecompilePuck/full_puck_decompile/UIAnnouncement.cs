using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class UIAnnouncement : UIComponent<UIAnnouncement>
{
	private VisualElement blueTeamScoreAnnouncement;

	private VisualElement redTeamScoreAnnouncement;

	private Label blueTeamScorePlayersLabel;

	private Label redTeamScorePlayersLabel;

	public void Initialize(VisualElement rootVisualElement)
	{
		container = rootVisualElement.Query<VisualElement>("AnnouncementContainer");
		blueTeamScoreAnnouncement = container.Query<VisualElement>("BlueTeamScoreAnnouncement");
		blueTeamScorePlayersLabel = blueTeamScoreAnnouncement.Query<Label>("PlayersLabel");
		redTeamScoreAnnouncement = container.Query<VisualElement>("RedTeamScoreAnnouncement");
		redTeamScorePlayersLabel = redTeamScoreAnnouncement.Query<Label>("PlayersLabel");
	}

	public override void Show()
	{
		if (MonoBehaviourSingleton<SettingsManager>.Instance.ShowGameUserInterface != 0)
		{
			base.Show();
		}
	}

	public override void Hide(bool ignoreAlwaysVisible = false)
	{
		base.Hide(ignoreAlwaysVisible);
		blueTeamScoreAnnouncement.style.visibility = Visibility.Hidden;
		redTeamScoreAnnouncement.style.visibility = Visibility.Hidden;
	}

	public void ShowBlueTeamScoreAnnouncement(float time, Player goalPlayer, Player assistPlayer, Player secondAssistPlayer)
	{
		if (!Application.isBatchMode)
		{
			StartCoroutine(IShowBlueTeamScoreAnnouncement(time, goalPlayer, assistPlayer, secondAssistPlayer));
		}
	}

	public void ShowRedTeamScoreAnnouncement(float time, Player goalPlayer, Player assistPlayer, Player secondAssistPlayer)
	{
		if (!Application.isBatchMode)
		{
			StartCoroutine(IShowRedTeamScoreAnnouncement(time, goalPlayer, assistPlayer, secondAssistPlayer));
		}
	}

	private IEnumerator IShowBlueTeamScoreAnnouncement(float time, Player goalPlayer, Player assistPlayer, Player secondAssistPlayer)
	{
		if (MonoBehaviourSingleton<SettingsManager>.Instance.ShowGameUserInterface == 0)
		{
			yield break;
		}
		string text = "";
		if ((bool)goalPlayer)
		{
			text += $"{goalPlayer.Username.Value}";
			if ((bool)assistPlayer)
			{
				text += $"<br><size=75%>{assistPlayer.Username.Value}";
			}
			if ((bool)secondAssistPlayer)
			{
				text += $" & {secondAssistPlayer.Username.Value}";
			}
		}
		blueTeamScorePlayersLabel.text = text;
		blueTeamScoreAnnouncement.style.visibility = Visibility.Visible;
		Show();
		yield return new WaitForSeconds(time);
		Hide();
	}

	private IEnumerator IShowRedTeamScoreAnnouncement(float time, Player goalPlayer, Player assistPlayer, Player secondAssistPlayer)
	{
		if (MonoBehaviourSingleton<SettingsManager>.Instance.ShowGameUserInterface == 0)
		{
			yield break;
		}
		string text = "";
		if ((bool)goalPlayer)
		{
			text += $"{goalPlayer.Username.Value}";
			if ((bool)assistPlayer)
			{
				text += $" + {assistPlayer.Username.Value}";
			}
			if ((bool)secondAssistPlayer)
			{
				text += $" & {secondAssistPlayer.Username.Value}";
			}
		}
		redTeamScorePlayersLabel.text = text;
		redTeamScoreAnnouncement.style.visibility = Visibility.Visible;
		Show();
		yield return new WaitForSeconds(time);
		Hide();
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
		return "UIAnnouncement";
	}
}
