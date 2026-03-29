using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Hover : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float maxForce = 40f;

	[SerializeField]
	private Vector3 raycastOffset = new Vector3(0f, 1f, 0f);

	[SerializeField]
	private float raycastDistance = 1.45f;

	[SerializeField]
	private LayerMask raycastLayerMask;

	[Space(20f)]
	[SerializeField]
	private float proportionalGain = 100f;

	[SerializeField]
	private float integralGain;

	[SerializeField]
	private float derivativeGain = 15f;

	[Space(20f)]
	public float TargetDistance = 1f;

	[HideInInspector]
	public bool IsGrounded;

	[HideInInspector]
	public Rigidbody Rigidbody;

	private PIDController pidController = new PIDController();

	private float currentDistance;

	private void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
	}

	private void FixedUpdate()
	{
		pidController.proportionalGain = proportionalGain;
		pidController.integralGain = integralGain;
		pidController.derivativeGain = derivativeGain;
		Vector3 vector = base.transform.position + raycastOffset;
		Vector3 down = Vector3.down;
		Debug.DrawRay(vector, down * raycastDistance, Color.black);
		if (Physics.Raycast(vector, down, out var hitInfo, raycastDistance, raycastLayerMask))
		{
			currentDistance = hitInfo.distance;
		}
		else
		{
			currentDistance = raycastDistance;
		}
		IsGrounded = currentDistance < raycastDistance;
		float value = pidController.Update(Time.fixedDeltaTime, currentDistance, TargetDistance);
		value = Mathf.Clamp(value, 0f, maxForce);
		if (NetworkManager.Singleton.IsServer)
		{
			Rigidbody.AddForce(Vector3.up * value, ForceMode.Acceleration);
		}
	}
}
