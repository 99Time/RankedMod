using UnityEngine;

namespace PuckAIPractice.AI;

public class GoalieSettings
{
	private static GoalieSettings _instanceBlue;

	private static GoalieSettings _instanceRed;

	public static GoalieSettings InstanceBlue
	{
		get
		{
			if (_instanceBlue == null)
			{
				_instanceBlue = new GoalieSettings();
			}
			return _instanceBlue;
		}
	}

	public static GoalieSettings InstanceRed
	{
		get
		{
			if (_instanceRed == null)
			{
				_instanceRed = new GoalieSettings();
			}
			return _instanceRed;
		}
	}

	public GoalieDifficulty Difficulty { get; private set; } = GoalieDifficulty.Hard;

	public float DashCooldown { get; private set; }

	public float DashCancelGrace { get; private set; }

	public float DashThreshold { get; private set; }

	public float CancelThreshold { get; private set; }

	public float ReactionTime { get; private set; }

	public float MaxRotationAngle { get; private set; }

	public float RotationSpeed { get; private set; }

	public float DistanceFromNet { get; set; }

	private GoalieSettings()
	{
		ApplyDifficulty(Difficulty);
	}

	public void ApplyDifficulty(GoalieDifficulty difficulty)
	{
		Difficulty = difficulty;
		Debug.Log((object)("Applying Difficutly: " + difficulty));
		switch (difficulty)
		{
		case GoalieDifficulty.Easy:
			DashCooldown = 1f;
			DashCancelGrace = 0.25f;
			DashThreshold = 1f;
			CancelThreshold = 0.08f;
			ReactionTime = 0.25f;
			MaxRotationAngle = 30f;
			RotationSpeed = 6f;
			DistanceFromNet = 1f;
			break;
		case GoalieDifficulty.Normal:
			DashCooldown = 0.6f;
			DashCancelGrace = 0.15f;
			DashThreshold = 0.4f;
			CancelThreshold = 0.05f;
			ReactionTime = 0.15f;
			MaxRotationAngle = 75f;
			RotationSpeed = 12f;
			DistanceFromNet = 1.2f;
			break;
		case GoalieDifficulty.Hard:
			DashCooldown = 0.2f;
			DashCancelGrace = 0.15f;
			DashThreshold = 0.2f;
			CancelThreshold = 0.05f;
			ReactionTime = 0.15f;
			MaxRotationAngle = 85f;
			RotationSpeed = 18f;
			DistanceFromNet = 1.4f;
			break;
		}
	}
}
