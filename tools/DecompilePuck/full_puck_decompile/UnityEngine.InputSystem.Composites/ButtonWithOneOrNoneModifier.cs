using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Utilities;

namespace UnityEngine.InputSystem.Composites;

[DisplayStringFormat("{modifier}+{button}")]
public class ButtonWithOneOrNoneModifier : InputBindingComposite<float>
{
	[InputControl]
	public int modifier;

	[InputControl]
	public int button;

	public bool ignoreOrder;

	public override float ReadValue(ref InputBindingCompositeContext context)
	{
		if (ModifierIsPressed(ref context) || modifier == 0)
		{
			return context.ReadValue<float>(button);
		}
		return 0f;
	}

	private bool ModifierIsPressed(ref InputBindingCompositeContext context)
	{
		bool flag = context.ReadValueAsButton(modifier);
		if (flag && !ignoreOrder)
		{
			double pressTime = context.GetPressTime(button);
			return context.GetPressTime(modifier) <= pressTime;
		}
		return flag;
	}

	public override float EvaluateMagnitude(ref InputBindingCompositeContext context)
	{
		return ReadValue(ref context);
	}
}
