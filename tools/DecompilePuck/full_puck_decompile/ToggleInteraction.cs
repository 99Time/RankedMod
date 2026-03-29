using UnityEngine.InputSystem;

public class ToggleInteraction : IInputInteraction
{
	private bool isToggled;

	public void Process(ref InputInteractionContext context)
	{
		if (!context.action.IsPressed())
		{
			return;
		}
		switch (context.phase)
		{
		case InputActionPhase.Waiting:
			if (!isToggled)
			{
				isToggled = true;
				context.Started();
			}
			break;
		case InputActionPhase.Started:
			if (isToggled)
			{
				isToggled = false;
				context.Canceled();
			}
			break;
		}
	}

	public void Reset()
	{
		isToggled = false;
	}
}
