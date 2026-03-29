using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private Goal goal;

	private void OnTriggerEnter(Collider collider)
	{
		Puck componentInParent = collider.GetComponentInParent<Puck>();
		if ((bool)componentInParent)
		{
			goal.Server_OnPuckEnterGoal(componentInParent);
		}
	}
}
