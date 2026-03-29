using UnityEngine.InputSystem;

public class DoublePressInteraction : IInputInteraction
{
	public float maxTapDuration = 0.2f;

	public float pressThreshold = 0.5f;

	public float releaseThreshold = 0.5f;

	private bool released;

	public void Process(ref InputInteractionContext context)
	{
		if (context.timerHasExpired)
		{
			context.Canceled();
			return;
		}
		switch (context.phase)
		{
		case InputActionPhase.Waiting:
			if (context.ReadValue<float>() > pressThreshold)
			{
				context.Started();
				context.SetTimeout(maxTapDuration);
			}
			break;
		case InputActionPhase.Started:
			if (released)
			{
				if (context.ReadValue<float>() > pressThreshold)
				{
					context.Performed();
				}
			}
			else if (context.ReadValue<float>() < releaseThreshold)
			{
				released = true;
			}
			break;
		}
	}

	public void Reset()
	{
		released = false;
	}
}
