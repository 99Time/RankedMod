using System;
using DG.Tweening;
using UnityEngine.UIElements;

public class ChatMessage
{
	public Label MessageLabel;

	public string Message;

	public float Time;

	public float CreateTime;

	public double Timestamp;

	public bool IsVisible;

	public const float FadeOutTime = 15f;

	public bool IsReady;

	private Tween showTween;

	private Tween hideTween;

	public float RemainingFadeTime => CreateTime + 15f - Time;

	public bool IsNew => RemainingFadeTime > 0f;

	public ChatMessage(Label messageLabel, float createTime, string message)
	{
		MessageLabel = messageLabel;
		Message = message;
		Time = createTime;
		CreateTime = createTime;
		Timestamp = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
		messageLabel.text = message;
		messageLabel.style.opacity = 0f;
		Show();
	}

	public void Show(float delay = 0f, bool autoHide = true)
	{
		if (IsVisible)
		{
			return;
		}
		IsVisible = true;
		showTween?.Kill();
		hideTween?.Kill();
		showTween = DOVirtual.DelayedCall(delay, delegate
		{
			IsReady = true;
			MessageLabel.style.opacity = 1f;
			if (autoHide)
			{
				Hide();
			}
		});
	}

	public void Hide()
	{
		if (IsVisible && IsReady)
		{
			IsVisible = false;
			showTween?.Kill();
			hideTween?.Kill();
			hideTween = DOVirtual.DelayedCall(IsNew ? RemainingFadeTime : 0f, delegate
			{
				MessageLabel.style.opacity = 0f;
			});
		}
	}

	public void Update(float time)
	{
		Time = time;
	}

	public void Dispose()
	{
		showTween?.Kill();
		hideTween?.Kill();
	}
}
