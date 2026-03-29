using UnityEngine;

public class GoalNetCollider : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float damping = 0.25f;

	[SerializeField]
	private float linearVelocityMaximumMagnitude = 2f;

	[SerializeField]
	private float angularVelocityMaximumMagnitude = 2f;

	private void OnCollisionEnter(Collision collision)
	{
		Puck componentInParent = collision.gameObject.GetComponentInParent<Puck>();
		if ((bool)componentInParent && !componentInParent.IsGrounded)
		{
			componentInParent.Rigidbody.linearVelocity *= 1f - damping;
			componentInParent.Rigidbody.angularVelocity *= 1f - damping;
			if (componentInParent.Rigidbody.linearVelocity.magnitude > linearVelocityMaximumMagnitude)
			{
				componentInParent.Rigidbody.linearVelocity = componentInParent.Rigidbody.linearVelocity.normalized * linearVelocityMaximumMagnitude;
			}
			if (componentInParent.Rigidbody.angularVelocity.magnitude > angularVelocityMaximumMagnitude)
			{
				componentInParent.Rigidbody.angularVelocity = componentInParent.Rigidbody.angularVelocity.normalized * angularVelocityMaximumMagnitude;
			}
		}
	}
}
