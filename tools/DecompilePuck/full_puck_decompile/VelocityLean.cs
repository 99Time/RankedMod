using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VelocityLean : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float linearForceMultiplier = 1f;

	[SerializeField]
	private float angularForceMultiplier = 6f;

	[HideInInspector]
	public Rigidbody Rigidbody;

	[HideInInspector]
	public float LinearIntensity = 1f;

	[HideInInspector]
	public float AngularIntensity = 1f;

	[HideInInspector]
	public bool Inverted;

	[HideInInspector]
	public bool UseWorldLinearVelocity;

	[HideInInspector]
	public Transform MovementDirection;

	private void Awake()
	{
		Rigidbody = GetComponent<Rigidbody>();
	}

	private void FixedUpdate()
	{
		float num = (UseWorldLinearVelocity ? Rigidbody.linearVelocity.magnitude : MovementDirection.InverseTransformVector(Rigidbody.linearVelocity).z);
		float y = MovementDirection.InverseTransformVector(Rigidbody.angularVelocity).y;
		if (NetworkManager.Singleton.IsServer)
		{
			Rigidbody.AddTorque(num * (Inverted ? (-base.transform.right) : base.transform.right) * linearForceMultiplier * LinearIntensity, ForceMode.Acceleration);
			Rigidbody.AddTorque((0f - y) * (Inverted ? (-base.transform.forward) : base.transform.forward) * angularForceMultiplier * AngularIntensity, ForceMode.Acceleration);
		}
	}
}
