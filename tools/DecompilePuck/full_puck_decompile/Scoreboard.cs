using System;
using TMPro;
using UnityEngine;

public class Scoreboard : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private TMP_Text minutesText;

	[SerializeField]
	private TMP_Text secondsText;

	[SerializeField]
	private TMP_Text periodText;

	[SerializeField]
	private TMP_Text blueScoreText;

	[SerializeField]
	private TMP_Text redScoreText;

	public void TurnOn()
	{
		((Component)(object)minutesText).gameObject.SetActive(value: true);
		((Component)(object)secondsText).gameObject.SetActive(value: true);
		((Component)(object)periodText).gameObject.SetActive(value: true);
		((Component)(object)blueScoreText).gameObject.SetActive(value: true);
		((Component)(object)redScoreText).gameObject.SetActive(value: true);
	}

	public void TurnOff()
	{
		((Component)(object)minutesText).gameObject.SetActive(value: false);
		((Component)(object)secondsText).gameObject.SetActive(value: false);
		((Component)(object)periodText).gameObject.SetActive(value: false);
		((Component)(object)blueScoreText).gameObject.SetActive(value: false);
		((Component)(object)redScoreText).gameObject.SetActive(value: false);
	}

	public void SetTime(int time)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(time);
		minutesText.text = timeSpan.ToString("mm");
		secondsText.text = timeSpan.ToString("ss");
	}

	public void SetPeriod(int period)
	{
		periodText.text = period.ToString();
	}

	public void SetBlueScore(int score)
	{
		blueScoreText.text = score.ToString();
	}

	public void SetRedScore(int score)
	{
		redScoreText.text = score.ToString();
	}
}
